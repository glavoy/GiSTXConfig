using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace generatexml
{
    public class XmlGenerator
    {
        public List<string> logstring = new List<string>();

        public void WriteXML(string xmlfilename, List<Question> QuestionList, string xmlPath)
        {
            try
            {
                if (xmlfilename.Substring(xmlfilename.Length - 3) == "_dd")
                {
                    xmlfilename = xmlfilename.Substring(0, xmlfilename.Length - 3);
                }
                else
                {
                    xmlfilename = xmlfilename.Substring(0, xmlfilename.Length - 4);
                }

                // These are strings for the first two of lines in the xml file
                string[] xmlStart = { "<?xml version = '1.0' encoding = 'utf-8'?>", "<survey>" };

                // Open a XML file and start writing lines of text to it
                using (StreamWriter outputFile = new StreamWriter(string.Concat(xmlPath, xmlfilename, ".xml")))
                {
                    // Write the first 2 lines to the XML file
                    foreach (string line in xmlStart)
                        outputFile.WriteLine(line);

                    // Write a blank line 
                    outputFile.WriteLine("\n");


                    // Iterate through each question object in the QuestionList list
                    // and write the necessary text to the XML file
                    foreach (Question question in QuestionList)
                    {
                        // Write the main part of the question
                        // Uses questionType, fieldName and fieldType
                                                outputFile.WriteLine(string.Concat("\t<question type = '", question.questionType,
                                                                                   "' fieldname = '", question.fieldName,
                                                                                   "' fieldtype = '", question.fieldType, "'>"));


                        // Write the text if it is not an automatic question
                        if (question.questionType != "automatic")
                            outputFile.WriteLine(string.Concat("\t\t<text>", question.questionText, "</text>"));


                        // The maximum characters if necessary
                        if (question.maxCharacters != "-9")
                            outputFile.WriteLine(string.Concat("\t\t<maxCharacters>", question.maxCharacters, "</maxCharacters>"));


                        if (!string.IsNullOrEmpty(question.uniqueCheckMessage))
                        {
                            outputFile.WriteLine("\t\t<unique_check>");
                            outputFile.WriteLine(string.Concat("\t\t\t<message>", question.uniqueCheckMessage, "</message>"));
                            outputFile.WriteLine("\t\t</unique_check>");
                        }


                        // Upper and Lower range (numeric check)
                        if (question.questionType != "date" && question.lowerRange != "-9")
                        {
                            outputFile.WriteLine("\t\t<numeric_check>");
                            outputFile.WriteLine(string.Concat("\t\t\t<values minvalue ='", question.lowerRange, "' maxvalue='", question.upperRange, "' other_values = '", question.lowerRange, "' message = 'Number must be between ", question.lowerRange, " and ", question.upperRange, "!'></values>"));
                            outputFile.WriteLine("\t\t</numeric_check>");
                        }

                        // Date range
                        if (question.questionType == "date")
                        {
                            outputFile.WriteLine("\t\t<date_range>");
                            outputFile.WriteLine(string.Concat("\t\t\t<min_date>", question.lowerRange, "</min_date>"));
                            outputFile.WriteLine(string.Concat("\t\t\t<max_date>", question.upperRange, "</max_date>"));
                            outputFile.WriteLine("\t\t</date_range>");
                        }

                        //  Logic Checks
                        if (question.logicCheck != "")
                        {
                            // New format: just output the logic check directly
                            outputFile.WriteLine("\t\t<logic_check>");
                            outputFile.WriteLine(GenerateLogicChecks(question.logicCheck));
                            outputFile.WriteLine("\t\t</logic_check>");
                        }

                        // Write responses if it is a radio or checkbox type question
                        if (question.questionType == "radio" || question.questionType == "checkbox" || question.questionType == "combobox")
                        {
                            outputFile.WriteLine("\t\t<responses>");
                            string[] responses = question.responses.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                            if (responses.Length == 0)
                            {
                                outputFile.WriteLine("\t\t\t<response></response>");
                            }
                            else
                            {
                                foreach (string response in responses)
                                {
                                    int index = response.IndexOf(@":");
                                    outputFile.WriteLine(string.Concat("\t\t\t<response value = '", response.Substring(0, index), "'>",
                                                                        response.Substring(index + 1).Trim(), "</response>"));
                                }
                            }

                            outputFile.WriteLine("\t\t</responses>");
                        }


                        // Skips
                        if (question.skip != "")
                        {
                            // This stores the text for the skip
                            string[] skips = question.skip.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                            // Lists to store preskips and postskips
                            List<string> preSkips = new List<string>();
                            List<string> postSkips = new List<string>();


                            // Populate the list for each type of skip
                            foreach (string skip in skips)
                            {
                                int index = skip.IndexOf(@":");

                                if (skip.Substring(0, index) == "preskip")
                                    preSkips.Add(skip);

                                if (skip.Substring(0, index) == "postskip")
                                    postSkips.Add(skip);
                            }


                            // Create text preskips
                            if (preSkips.Count > 0)
                            {
                                outputFile.WriteLine("\t\t<preskip>");
                                foreach (string preSkip in preSkips)
                                {
                                    // Call the GenerateSkips() function
                                    outputFile.WriteLine(GenerateSkips(preSkip, "preSkip"));
                                }
                                outputFile.WriteLine("\t\t</preskip>");
                            }


                            // Create text postskips
                            if (postSkips.Count > 0)
                            {
                                outputFile.WriteLine("\t\t<postskip>");
                                // Call the GenerateSkips() function
                                foreach (string postSkip in postSkips)
                                {
                                    outputFile.WriteLine(GenerateSkips(postSkip, "postSkip"));
                                }
                                outputFile.WriteLine("\t\t</postskip>");
                            }
                        }



                        // Don't know
                        if (question.dontKnow == "TRUE" || question.dontKnow == "True")
                            outputFile.WriteLine("\t\t<dont_know>-7</dont_know>");

                        // Refuse to answer
                        if (question.refuse == "TRUE" || question.refuse == "True")
                            outputFile.WriteLine("\t\t<refuse>-8</refuse>");

                        // Not applicable
                        if (question.na == "TRUE" || question.na == "True")
                            outputFile.WriteLine("\t\t<na>-6</na>");

                        // Close off the question
                        outputFile.WriteLine("\t</question>");
                        outputFile.WriteLine("\n");
                    }

                    // The last 'info' question ending every survey
                    string[] xmlEnd = {"\t<question type = 'information' fieldname = 'end_of_questions' fieldtype = 'n/a'>",
                                   "\t\t<text>Press the 'Finish' button to save the data.</text >", "\t</question>" };
                    foreach (string line in xmlEnd)
                        outputFile.WriteLine(line);

                    outputFile.WriteLine("\n");
                    outputFile.WriteLine("</survey>");
                }
            }


            // Error handling in caase we could not create the XML file
            catch (Exception ex)
            {
                MessageBox.Show("ERROR - Writing to XML file: Could not create XML file " + xmlfilename + " Ensure path is correct." + ex.Message);
                logstring.Add("ERROR - Writing to XML file: Could not create XML file " + xmlfilename + " Ensure path is correct." + ex.Message);
            }
        }



        //////////////////////////////////////////////////////////////////////
        // Function to generate the text for the skips
        //////////////////////////////////////////////////////////////////////
        private string GenerateSkips(string skip, string skipType)
        {
            // Number of initial characters depending on whether it's a preskip or postskip
            int lenSkip = skipType == "postSkip" ? 13 : 12;


            // Create a list to store the index of each 'space' in the skip text
            var spaceIndices = new List<int>();

            // Populate the spaceIndices list
            for (int i = 0; i < skip.Length; i++)
                if (skip[i] == ' ') spaceIndices.Add(i);


            // Get the name of the field to check for skip
            string fieldname_to_check = skip.Substring(lenSkip, spaceIndices[2] - spaceIndices[1] - 1);

            // Variables to store the condition and the value of the skip
            string condition;
            string value;

            // If there are 9 spaces, then we know that the condition is 'does not contain'
            if (spaceIndices.Count == 9)
            {
                // Get the condition
                condition = "does not contain";
                // Get the value
                value = skip.Substring(spaceIndices[5] + 1, spaceIndices[6] - spaceIndices[5] - 2);
            }
            // Check if the skip has 'contains'
            else if (skip.Contains("contains"))
            {
                // Get the condition
                condition = "contains";
                // Get the value
                value = skip.Substring(spaceIndices[3] + 1, spaceIndices[4] - spaceIndices[3] - 2);
            }
            // Skip does not have 'does not contain' or 'contains'
            else
            {
                // Get the condition
                condition = skip.Substring(spaceIndices[2] + 1, spaceIndices[3] - spaceIndices[2] - 1);

                // Replace '<' and '>' symbols, if necessary
                condition = condition.Replace("<", "&lt;");
                condition = condition.Replace(">", "&gt;");

                // Get the value
                value = skip.Substring(spaceIndices[3] + 1, spaceIndices[4] - spaceIndices[3] - 2);
            }

            // Get the field name to skip to
            string fieldname_to_skip_to = skip.Substring(spaceIndices[spaceIndices.Count - 1] + 1);

            // Build the string and return it
            return string.Concat("\t\t\t<skip fieldname='", fieldname_to_check,
                                 "' condition = '", condition,
                                 "' response='", value,
                                 "' response_type='fixed' skiptofieldname ='",
                                 fieldname_to_skip_to, "'></skip>");
        }



        //////////////////////////////////////////////////////////////////////
        // Function to generate the text for the logic checks
        //////////////////////////////////////////////////////////////////////
        private string GenerateLogicChecks(string logicCheck)
        {
            // New format: expression; 'error message'
            // Example: tabletnum2 != tabletnum; 'This does not match your previous entry!'

            // Split by semicolon to get expression and message
            string[] parts = logicCheck.Split(new char[] { ';' }, 2);
            string expression = parts[0].Trim();
            string message = parts[1].Trim();

            // Replace operators with XML entities
            expression = expression.Replace("!=", "&lt;&gt;");
            expression = expression.Replace("<>", "&lt;&gt;");
            expression = expression.Replace("<=", "&lt;=");
            expression = expression.Replace(">=", "&gt;=");
            // Replace individual < and > that aren't part of <= or >=
            expression = Regex.Replace(expression, @"(?<!&lt;)(?<!&gt;)<(?!=)", "&lt;");
            expression = Regex.Replace(expression, @"(?<!&lt;=)(?<!&gt;=)>(?!=)", "&gt;");

            StringBuilder result = new StringBuilder();

            // Check if expression contains 'or' - if so, format it across multiple lines
            if (expression.Contains(" or "))
            {
                string[] orParts = expression.Split(new string[] { " or " }, StringSplitOptions.None);

                for (int i = 0; i < orParts.Length; i++)
                {
                    result.Append("\t\t\t");
                    result.Append(orParts[i].Trim());

                    if (i < orParts.Length - 1)
                    {
                        result.Append(" or");
                        result.AppendLine();
                    }
                }
                result.AppendLine(";");
                result.Append("\t\t\t");
                result.Append(message);
            }
            else
            {
                // Single line format
                result.Append("\t\t\t");
                result.Append(expression);
                result.Append("; ");
                result.Append(message);
            }

            return result.ToString();
        }
    }
}