using System.Globalization;

namespace OpenOnGitHub.Providers
{
    internal sealed class BitBucketUrlProvider : IGitUrlProvider
    {
        public string GetUrl(GitRepository repository, string filePath, GitHubUrlType urlType, 
            SelectedRange selectedRange)
        {
            var fileIndexPath = repository.GetFileIndexPath(filePath);
            var repositoryTarget = repository.GetGitHubTargetPath(urlType);

            var uri = $"{repository.UrlRoot}/src/{repositoryTarget}/{fileIndexPath}";

            uri += GetSelection(selectedRange);

            return uri;
        }

        public string GetSelection(SelectedRange selectedRange)
        {
            var selection = string.Empty;

            if (selectedRange == SelectedRange.Empty)
            {
                return selection;
            }

            selection += "#lines-" + selectedRange.TopLine.ToString(CultureInfo.InvariantCulture);

            if (selectedRange.TopLine != selectedRange.BottomLine)
            {
                selection += ":" + selectedRange.BottomLine.ToString(CultureInfo.InvariantCulture);
            }

            return selection;
        }

        public bool IsUrlTypeAvailable(GitHubUrlType urlType) => true;
    }
}