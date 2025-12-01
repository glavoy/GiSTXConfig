using System.Collections.Generic;

namespace generatexml
{
    public enum ResponseSourceType
    {
        Static,
        Csv,
        Database
    }

    public class Filter
    {
        public string Column { get; set; }
        public string Value { get; set; }
        public string Operator { get; set; } = "="; // Default operator
    }

    public class Question
    {
        public string fieldName;
        public string questionType;
        public string fieldType;
        public string questionText;
        public string maxCharacters;

        // Original responses field for static options
        public string responses;

        // New fields for dynamic responses
        public ResponseSourceType ResponseSourceType { get; set; } = ResponseSourceType.Static;
        public string ResponseSourceFile { get; set; } // For CSV
        public string ResponseSourceTable { get; set; } // For Database
        public List<Filter> ResponseFilters { get; set; } = new List<Filter>();
        public string ResponseDisplayColumn { get; set; }
        public string ResponseValueColumn { get; set; }
        public bool? ResponseDistinct { get; set; } // Nullable bool to represent absence
        public string ResponseEmptyMessage { get; set; }
        public string ResponseDontKnowValue { get; set; }
        public string ResponseDontKnowLabel { get; set; }
        public string ResponseNotInListValue { get; set; }
        public string ResponseNotInListLabel { get; set; }


        public string lowerRange;
        public string upperRange;
        public string logicCheck;
        public string uniqueCheckMessage = "";
        public string dontKnow;
        public string refuse;
        public string na;
        public string skip;
    }
}
