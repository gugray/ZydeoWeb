using System.Collections.Generic;

namespace ZDO.CHSite.Entities
{
    public class HeadwordProblem
    {
        public bool Error;
        public string Message;
        public HeadwordProblem() { }
        public HeadwordProblem(bool error, string message)
        {
            Error = error;
            Message = message;
        }
    }

    public class EditEntryPreviewResult
    {
        public List<HeadwordProblem> ErrorsSimp = new List<HeadwordProblem>();
        public List<HeadwordProblem> ErrorsTrad = new List<HeadwordProblem>();
        public List<HeadwordProblem> ErrorsPinyin = new List<HeadwordProblem>();
        public string PreviewHtml = null;
    }
}
