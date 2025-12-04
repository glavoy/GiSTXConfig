using System.Collections.Generic;

namespace generatexml
{
    public enum ResponseSourceType
    {
        Static,
        Csv,
        Database
    }

    public enum CalculationType
    {
        None,
        Query,
        Case,
        Constant,
        Lookup,
        Math,
        Concat
    }

    public class Filter
    {
        public string Column { get; set; }
        public string Value { get; set; }
        public string Operator { get; set; } = "="; // Default operator
    }

    public class CalculationParameter
    {
        public string Name { get; set; }        // "@hhid"
        public string FieldName { get; set; }   // "hhid"
    }

    public class CaseCondition
    {
        public string Field { get; set; }
        public string Operator { get; set; }
        public string Value { get; set; }
        public CalculationPart Result { get; set; }
    }

    public class CalculationPart
    {
        public CalculationType Type { get; set; }

        // For constant parts
        public string ConstantValue { get; set; }

        // For lookup parts
        public string LookupField { get; set; }

        // For query parts
        public string QuerySql { get; set; }
        public List<CalculationParameter> QueryParameters { get; set; } = new List<CalculationParameter>();

        // For math parts (nested)
        public string MathOperator { get; set; }
        public List<CalculationPart> Parts { get; set; } = new List<CalculationPart>();

        // For concat parts (nested)
        public string ConcatSeparator { get; set; }
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

        // Automatic calculation fields
        public CalculationType CalculationType { get; set; } = CalculationType.None;

        // For query calculations
        public string CalculationQuerySql { get; set; }
        public List<CalculationParameter> CalculationQueryParameters { get; set; } = new List<CalculationParameter>();

        // For case calculations
        public List<CaseCondition> CalculationCaseConditions { get; set; } = new List<CaseCondition>();
        public CalculationPart CalculationCaseElse { get; set; }

        // For constant calculations
        public string CalculationConstantValue { get; set; }

        // For lookup calculations
        public string CalculationLookupField { get; set; }

        // For math calculations
        public string CalculationMathOperator { get; set; }
        public List<CalculationPart> CalculationMathParts { get; set; } = new List<CalculationPart>();

        // For concat calculations
        public string CalculationConcatSeparator { get; set; }
        public List<CalculationPart> CalculationConcatParts { get; set; } = new List<CalculationPart>();


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
