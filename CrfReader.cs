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

            // BULK READ: Load entire worksheet data into memory for fast access
            object[,] data = range.Value2 as object[,];
            if (data == null) return crfs;

            int numRows = data.GetLength(0);

            for (int rowCount = 2; rowCount <= numRows; rowCount++)
            {
                var crf = new Crf();

                crf.display_order = GetNullableInt(data, rowCount, 1);
                crf.tablename = GetString(data, rowCount, 2);
                crf.displayname = GetString(data, rowCount, 3);
                crf.primarykey = GetString(data, rowCount, 4);

                string idconfigJson = GetString(data, rowCount, 5);
                if (!string.IsNullOrEmpty(idconfigJson))
                {
                    crf.idconfig = Newtonsoft.Json.JsonConvert.DeserializeObject<IdConfig>(idconfigJson);
                }

                crf.isbase = GetNullableInt(data, rowCount, 6);
                crf.linkingfield = GetString(data, rowCount, 7);
                crf.parenttable = GetString(data, rowCount, 8);
                crf.incrementfield = GetString(data, rowCount, 9);
                crf.requireslink = GetNullableInt(data, rowCount, 10);
                crf.repeat_count_field = GetString(data, rowCount, 11);
                crf.auto_start_repeat = GetNullableInt(data, rowCount, 12);
                crf.repeat_enforce_count = GetNullableInt(data, rowCount, 13);
                crf.display_fields = GetString(data, rowCount, 14);
                crf.entry_condition = GetString(data, rowCount, 15);

                crfs.Add(crf);
            }

            return crfs;
        }

        private string GetString(object[,] data, int row, int col)
        {
            if (row < 1 || row > data.GetLength(0) || col < 1 || col > data.GetLength(1))
                return null;
            var value = data[row, col]?.ToString()?.Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private int? GetNullableInt(object[,] data, int row, int col)
        {
            var value = GetString(data, row, col);
            if (int.TryParse(value, out int result))
            {
                return result;
            }
            return null;
        }
    }
}
