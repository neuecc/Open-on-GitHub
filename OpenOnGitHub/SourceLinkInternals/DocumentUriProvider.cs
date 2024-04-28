using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace OpenOnGitHub.SourceLinkInternals
{
    internal sealed class DocumentUriProvider
    {
        private static readonly Guid SourceLinkCustomDebugInformationId = new("CC110556-A091-4D38-9FEC-25AB9A351A6A");
        private static readonly Guid EmbeddedSourceCustomDebugInformationId = new("0E8A571B-6926-466E-B4AD-8AB04611F5FE");
        private static readonly byte[] CrlfBytes = [(byte)'\r', (byte)'\n'];
        private readonly List<string> _errors = [];

        internal record DocumentInfo
        {
            public string ContainingFile {  get; set; }
            public string Name { get; set; }
            public string Uri { get; set; }
            public bool IsEmbedded { get; set; }
            public ImmutableArray<byte> Hash { get; set; }
            public Guid HashAlgorithm { get; set; }
        }

        internal record PdbMetadata : IDisposable
        {
            public string FilePath { get; set; }

            public MetadataReaderProvider Provider { get; set; }

            public void Dispose()
            {
                Provider?.Dispose();
            }
        }

        public static string GetDocumentUri(string pdbPath, string documentPath)
        {
            try
            {
                var doc = GetDocumentByPdbAndFilePath(pdbPath, documentPath);

                var uri = doc?.Uri;

                return string.IsNullOrEmpty(uri) ? null : ReplaceGitHubRawUriWithUi(uri);
            }
            catch (Exception)
            { 
                return null;
            }        
        }

        private static string ReplaceGitHubRawUriWithUi(string uri)
        {
            var uriBuilder = new UriBuilder(uri);

            if (uriBuilder.Host != "raw.githubusercontent.com")
            {
                return uri;
            }

            uriBuilder.Host = "github.com";
            var pathParts = uriBuilder.Path.TrimStart('/').Split('/').ToList();
            pathParts.Insert(2, "blob");
            uriBuilder.Path = string.Join("/", pathParts);
            return uriBuilder.Uri.ToString();

        }

        private void ReportError(string message)
        {
            _errors.Add(message);
        }

        private static DocumentInfo GetDocumentByPdbAndFilePath(string pdbPath, string documentPath)
        {
            var provider = new DocumentUriProvider();
            var documents = provider.ReadAndResolveDocuments(pdbPath).ToList();

            if (provider._errors.Count > 0)
            {
                return null;
            }

            var docName = Path.GetFileName(documentPath);
            var filteredDocs = documents.FindAll(x => x.Name.EndsWith(docName));

            var hashAlgos = filteredDocs.Select(x => x.HashAlgorithm).Distinct().ToArray();

            var documentContent = File.ReadAllBytes(documentPath);

            var hashes = hashAlgos.SelectMany(algo => GetHashes(algo, documentContent)).ToArray();

            var singleFile = filteredDocs.FindAll(fd => hashes.Any(h => fd.Hash.SequenceEqual(h ?? [])));

            return singleFile.FirstOrDefault();
        }

        private static IEnumerable<byte[]> GetHashes(Guid algo, byte[] docContent)
        {
            var algorithmName = HashAlgorithmGuids.GetName(algo);
            using var incrementalHash = IncrementalHash.CreateHash(algorithmName);
            incrementalHash.AppendData(docContent);
            yield return incrementalHash.GetHashAndReset();
            yield return TryCalculateHashWithLineBreakSubstituted(docContent, incrementalHash);
        }

        private static byte[] TryCalculateHashWithLineBreakSubstituted(byte[] content, IncrementalHash incrementalHash)
        {
            int index = 0;
            while (true)
            {
                int lf = Array.IndexOf(content, (byte)'\n', index);
                if (lf < 0)
                {
                    incrementalHash.AppendData(content, index, content.Length - index);
                    return incrementalHash.GetHashAndReset();
                }

                if (index - 1 >= 0 && content[index - 1] == (byte)'\r')
                {
                    // The file either has CRLF line endings or mixed line endings.
                    // In either case there is no need to substitute LF to CRLF.
                    _ = incrementalHash.GetHashAndReset();
                    return null;
                }

                incrementalHash.AppendData(content, index, lf - index);
                incrementalHash.AppendData(CrlfBytes);
                index = lf + 1;
            }
        }

        private PdbMetadata GetPdbMetadata(string path)
        {
            var filePath = path;

            try
            {
                if (string.Equals(Path.GetExtension(path), ".pdb", StringComparison.OrdinalIgnoreCase))
                {
                    return new PdbMetadata
                    {
                        Provider = MetadataReaderProvider.FromPortablePdbStream(File.OpenRead(path)),
                        FilePath = filePath
                    };
                }

                using var peReader = new PEReader(File.OpenRead(path));
                if (peReader.TryOpenAssociatedPortablePdb(path, pdbFileStreamProvider: File.OpenRead, out var pdbReaderProvider, out filePath))
                {
                    return new PdbMetadata
                    {
                        Provider = pdbReaderProvider,
                        FilePath = filePath ?? path
                    };
                }
            }
            catch (Exception e)
            {
                ReportError($"Error reading '{filePath}': {e.Message}");
            }

            return null;
        }

        private IEnumerable<DocumentInfo> ReadAndResolveDocuments(string path)
        {
            using var pdbMetadata = GetPdbMetadata(path);

            if (pdbMetadata == null)
            {
                ReportError($"Symbol information not found for '{path}'.");
                yield break;
            }

            var filePath = pdbMetadata.FilePath;
            var metadataReader = pdbMetadata.Provider.GetMetadataReader();


            var documents = new List<(string name, ImmutableArray<byte> hash, Guid hashAlgorithm, bool isEmbedded)>();
            bool hasUnembeddedDocument = false;

            foreach (var documentHandle in metadataReader.Documents)
            {
                var document = metadataReader.GetDocument(documentHandle);
                var name = metadataReader.GetString(document.Name);
                var isEmbedded = HasCustomDebugInformation(metadataReader, documentHandle, EmbeddedSourceCustomDebugInformationId);
                var hash = metadataReader.GetBlobContent(document.Hash);
                var hashAlgorithm = metadataReader.GetGuid(document.HashAlgorithm);

                documents.Add((name, hash, hashAlgorithm, isEmbedded));

                if (!isEmbedded)
                {
                    hasUnembeddedDocument = true;
                }
            }

            SourceLinkMap sourceLinkMap = default;
            if (hasUnembeddedDocument)
            {
                var sourceLink = ReadSourceLink(metadataReader);
                if (sourceLink == null)
                {
                    ReportError("Source Link record not found.");
                    yield break;
                }

                try
                {
                    sourceLinkMap = SourceLinkMap.Parse(sourceLink);
                }
                catch (Exception e)
                {
                    ReportError($"Error reading Source Link: {e.Message}");
                    yield break;
                }
            }

            foreach (var (name, hash, hashAlgorithm, isEmbedded) in documents)
            {
                var uri = isEmbedded ? null : sourceLinkMap.TryGetUri(name, out var mappedUri) ? mappedUri : null;
                yield return new DocumentInfo
                {
                    ContainingFile = filePath,
                    Name = name,
                    Uri = uri,
                    IsEmbedded = isEmbedded,
                    Hash = hash,
                    HashAlgorithm = hashAlgorithm
                };
            }
        }

        private static IEnumerable<CustomDebugInformation> GetCustomDebugInformation(MetadataReader metadataReader, EntityHandle handle, Guid kind)
        {
            return metadataReader.GetCustomDebugInformation(handle)
                .Select(metadataReader.GetCustomDebugInformation)
                .Where(cdi => metadataReader.GetGuid(cdi.Kind) == kind);
        }

        private static bool HasCustomDebugInformation(MetadataReader metadataReader, EntityHandle handle, Guid kind)
        {
            return GetCustomDebugInformation(metadataReader, handle, kind).Any();
        }

        private static BlobReader GetCustomDebugInformationReader(MetadataReader metadataReader, EntityHandle handle, Guid kind)
        {
            var debugInformationReader = GetCustomDebugInformation(metadataReader, handle, kind)
                .Select(cdi => metadataReader.GetBlobReader(cdi.Value))
                .FirstOrDefault();

            return debugInformationReader;
        }

        private static string ReadSourceLink(MetadataReader metadataReader)
        {
            var blobReader = GetCustomDebugInformationReader(metadataReader, EntityHandle.ModuleDefinition, SourceLinkCustomDebugInformationId);
            return blobReader.Length > 0 ? blobReader.ReadUTF8(blobReader.Length) : null;
        }
    }
}