namespace OpenOnGitHub
{
    public record SelectedRange
    {
        public int TopLine { get; set; }
        public int BottomLine { get; set; }
        public int TopColumn { get; set; }
        public int BottomColumn { get; set; }

        public static SelectedRange Empty { get; } = new();
    }
}