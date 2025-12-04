using System;
using System.Collections.Generic;
using GistConfigX;
using Excel = Microsoft.Office.Interop.Excel;

namespace generatexml
{
    public class CrfReader
    {
        public List<Crf> ReadCrfsWorksheet(Excel.Worksheet worksheet)
        {
            var crfs = new List<Crf>();
            var range = worksheet.UsedRange;
            int numRows = range.Rows.Count;

            for (int rowCount = 2; rowCount <= numRows; rowCount++)
            {
                var crf = new Crf();

                crf.display_order = GetNullableInt(range, rowCount, 1);
                crf.tablename = GetString(range, rowCount, 2);
                crf.displayname = GetString(range, rowCount, 3);
                crf.primarykey = GetString(range, rowCount, 4);

                string idconfigJson = GetString(range, rowCount, 5);
                if (!string.IsNullOrEmpty(idconfigJson))
                {
                    crf.idconfig = Newtonsoft.Json.JsonConvert.DeserializeObject<IdConfig>(idconfigJson);
                }

                crf.isbase = GetNullableInt(range, rowCount, 6);
                crf.linkingfield = GetString(range, rowCount, 7);
                crf.parenttable = GetString(range, rowCount, 8);
                crf.incrementfield = GetString(range, rowCount, 9);
                crf.requireslink = GetNullableInt(range, rowCount, 10);
                crf.repeat_count_field = GetString(range, rowCount, 11);
                crf.auto_start_repeat = GetNullableInt(range, rowCount, 12);
                crf.repeat_enforce_count = GetNullableInt(range, rowCount, 13);
                crf.display_fields = GetString(range, rowCount, 14);
                crf.entry_condition = GetString(range, rowCount, 15);

                crfs.Add(crf);
            }

            return crfs;
        }

        private string GetString(Excel.Range range, int row, int col)
        {
            var value = range.Cells[row, col]?.Value2?.ToString();
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private int? GetNullableInt(Excel.Range range, int row, int col)
        {
            var value = GetString(range, row, col);
            if (int.TryParse(value, out int result))
            {
                return result;
            }
            return null;
        }
    }
}
