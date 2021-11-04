using System.Collections.Generic;

namespace ZDO.CHSite.Entities
{
    public class HeadwordProblem
    {
        public bool Error { get; set; }
        public string Message { get; set; }
        public HeadwordProblem() { }
        public HeadwordProblem(bool error, string message)
        {
            Error = error;
            Message = message;
        }
    }

    public class EditEntryPreviewResult
    {
        public List<HeadwordProblem> ErrorsSimp { get; set; } = new List<HeadwordProblem>();
        public List<HeadwordProblem> ErrorsTrad { get; set; } = new List<HeadwordProblem>();
        public List<HeadwordProblem> ErrorsPinyin { get; set; } = new List<HeadwordProblem>();
        public string PreviewHtml { get; set; } = null;
    }
}
