//using System.Net;

//namespace OpenOnGitHub.Providers;

//internal sealed class GithubUrlProvider : IGitUrlProvider
//{
//    public string GetUrl(GitRepository repository, string filePath, GitHubUrlType urlType, SelectedRange selectedRange)
//    {
//        var fileIndexPath = repository.GetFileIndexPath(filePath);
//        var repositoryTarget = repository.GetGitHubTargetPath(urlType);

//        // line selection
//        var fragment = selectedRange != SelectedRange.Empty
//            ? selectedRange.TopLine == selectedRange.BottomLine
//                ? $"#L{selectedRange.TopLine}"
//                : $"#L{selectedRange.TopLine}-L{selectedRange.BottomLine}"
//            : string.Empty;

//        var fileUrl = $"{repository.UrlRoot}/blob/{WebUtility.UrlEncode(repositoryTarget)}/{fileIndexPath}{fragment}";

//        return fileUrl;
//    }

//    public bool IsUrlTypeAvailable(GitHubUrlType urlType) => true;
//}