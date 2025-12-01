using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Excel = Microsoft.Office.Interop.Excel;

namespace generatexml
{
    public class DatabaseManager
    {
        public List<string> logstring = new List<string>();

        public void CreateSQLiteDatabase(string db_path)
        {
            try
            {
                // Delete the SQLite database if it exists
                if (File.Exists(db_path))
                {
                    File.Delete(db_path);
                }

                // Create the SQLite database
                SQLiteConnection.CreateFile(db_path);

                // Optionally, you can open a connection to verify
                using (var connection = new SQLiteConnection($"Data Source={db_path};Version=3;"))
                {
                    connection.Open();
                    // Database created successfully
                }

                // Create the form changes table
                CreateFormChanges(db_path);
            }
            catch (Exception ex)
            {
                MessageBox.Show("ERROR: Could not create SQLite database: " + ex.Message);
                logstring.Add("ERROR: Could not create SQLite database: " + ex.Message);
            }
        }

        public void CreateTableInDatabase(string tablename, string db_path, List<Question> QuestionList)
        {
            try
            {
                tablename = tablename.Substring(0, tablename.Length - 3);

                using (var connection = new SQLiteConnection($"Data Source={db_path};Version=3;"))
                {
                    connection.Open();

                    // Build CREATE TABLE statement
                    StringBuilder createTableQuery = new StringBuilder();
                    createTableQuery.Append($"CREATE TABLE IF NOT EXISTS [{tablename}] (");

                    List<string> columns = new List<string>();

                    // Create a field name for each question
                    foreach (Question question in QuestionList)
                    {
                        // Don't need to create a field for 'information' questions
                        if (question.questionType != "information" && question.fieldType != "n/a")
                        {
                            string columnDef = $"[{question.fieldName}] ";

                            // Map field types to SQLite types
                            switch (question.fieldType)
                            {
                                case "text_integer":
                                case "integer":
                                    columnDef += "INTEGER";
                                    break;
                                case "text":
                                case "text_id":
                                case "phone_num":
                                case "hourmin":
                                    columnDef += "TEXT";
                                    break;
                                case "text_decimal":
                                    columnDef += "REAL";
                                    break;
                                case "date":
                                case "datetime":
                                    columnDef += "TEXT"; // SQLite stores dates as TEXT, REAL, or INTEGER
                                    break;
                                default:
                                    columnDef += "TEXT";
                                    break;
                            }

                            columns.Add(columnDef);
                        }
                    }

                    createTableQuery.Append(string.Join(", ", columns));
                    createTableQuery.Append(")");

                    using (var command = new SQLiteCommand(createTableQuery.ToString(), connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("ERROR - Database: Could not create " + tablename + " in database. " + ex.Message);
                logstring.Add("ERROR - Database: Could not create " + tablename + " in database. " + ex.Message);
            }
        }

        public void CreateFormChanges(string db_path)
        {
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={db_path};Version=3;"))
                {
                    connection.Open();

                    string createTableQuery = @"
                        CREATE TABLE formchanges (
                            changeid     INTEGER PRIMARY KEY AUTOINCREMENT,
                            tablename    TEXT NOT NULL,
                            fieldname    TEXT NOT NULL,
                            uniqueid     TEXT NOT NULL,
                            oldvalue     TEXT,
                            newvalue     TEXT,
                            changed_at   DATETIME DEFAULT (CURRENT_TIMESTAMP)
                        )";

                    using (var command = new SQLiteCommand(createTableQuery, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("ERROR: Could not create 'formchanges' table in database. " + ex.Message);
                logstring.Add("ERROR: Could not create 'formchanges' table in database. " + ex.Message);
            }
        }

        public void CreateCrfsTable(string db_path)
        {
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={db_path};Version=3;"))
                {
                    connection.Open();

                    string createTableQuery = @"
                                CREATE TABLE crfs (
                                  display_order INTEGER DEFAULT 0, 
                                  tablename TEXT,
                                  primarykey TEXT,
                                  displayname TEXT,
                                  isbase INTEGER DEFAULT 0,
                                  linkingfield TEXT,
                                  parenttable TEXT,
                                  incrementfield TEXT,
                                  requireslink INTEGER DEFAULT 0,
                                  idconfig TEXT,
                                  repeat_count_field TEXT, 
                                  repeat_count_source TEXT, 
                                  auto_start_repeat INTEGER, 
                                  repeat_enforce_count INTEGER,
                                  display_fields TEXT
                                )";

                    using (var command = new SQLiteCommand(createTableQuery, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("ERROR: Could not create 'crfs' table in database. " + ex.Message);
                logstring.Add("ERROR: Could not create 'crfs' table in database. " + ex.Message);
            }
        }

        public void AddDataToTable(Excel.Worksheet crf_ws, string db_path)
        {
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={db_path};Version=3;"))
                {
                    connection.Open();

                    string insertQuery =
                        "INSERT INTO crfs (" +
                        "display_order, tablename, primarykey, displayname, isbase, linkingfield, parenttable, " +
                        "incrementfield, requireslink, idconfig, repeat_count_field, " +
                        "auto_start_repeat, repeat_enforce_count, display_fields) " +
                        "VALUES (@display_order, @tablename, @primarykey, @displayname, @isbase, @linkingfield, @parenttable, " +
                        "@incrementfield, @requireslink, @idconfig, @repeat_count_field, " +
                        "@auto_start_repeat, @repeat_enforce_count, @display_fields)";

                    Excel.Range usedRange = crf_ws.UsedRange;
                    int lastRow = usedRange.Rows.Count;

                    using (var transaction = connection.BeginTransaction())
                    using (var insertCommand = new SQLiteCommand(insertQuery, connection))
                    {
                        // Ensure this command participates in the transaction
                        insertCommand.Transaction = transaction;

                        object GetCellValue(int r, int c)
                        {
                            try
                            {
                                var val = ((Excel.Range)usedRange.Cells[r, c]).Value2;
                                if (val is string s && string.IsNullOrWhiteSpace(s)) return null;
                                return val;
                            }
                            catch
                            {
                                return null;
                            }
                        }

                        int ToInt(object o, int defaultValue = 0)
                        {
                            if (o == null) return defaultValue;
                            if (o is double d) return Convert.ToInt32(d);
                            if (o is int i) return i;
                            if (int.TryParse(o.ToString(), out int parsed)) return parsed;
                            return defaultValue;
                        }

                        for (int row = 2; row <= lastRow; row++) // assume header is the first row of usedRange
                        {
                            insertCommand.Parameters.Clear();

                            // Excel columns (per your order):
                            // 1 display_order
                            // 2 tablename
                            // 3 primarykey
                            // 4 displayname
                            // 5 isbase
                            // 6 linkingfield
                            // 7 parenttable
                            // 8 incrementfield
                            // 9 requireslink
                            // 10 idconfig
                            // 11 repeat_count_field
                            // 12 auto_start_repeat
                            // 13 repeat_enforce_count
                            // 14 display_fields

                            var rawDisplayOrder = GetCellValue(row, 1);
                            var rawTablename = GetCellValue(row, 2);
                            var rawPrimaryKey = GetCellValue(row, 3);
                            var rawDisplayName = GetCellValue(row, 4);
                            var rawIsBase = GetCellValue(row, 5);
                            var rawLinkingField = GetCellValue(row, 6);
                            var rawParentTable = GetCellValue(row, 7);
                            var rawIncrementField = GetCellValue(row, 8);
                            var rawRequiresLink = GetCellValue(row, 9);
                            var rawIdConfig = GetCellValue(row, 10);
                            var rawRepeatCountField = GetCellValue(row, 11);
                            var rawAutoStartRepeat = GetCellValue(row, 13);
                            var rawRepeatEnforceCount = GetCellValue(row, 14);
                            var rawDisplayFields = GetCellValue(row, 15);

                            insertCommand.Parameters.AddWithValue("@display_order", ToInt(rawDisplayOrder, 0));
                            insertCommand.Parameters.AddWithValue("@tablename", (object)rawTablename ?? DBNull.Value);
                            insertCommand.Parameters.AddWithValue("@primarykey", (object)rawPrimaryKey ?? DBNull.Value);
                            insertCommand.Parameters.AddWithValue("@displayname", (object)rawDisplayName ?? DBNull.Value);
                            insertCommand.Parameters.AddWithValue("@isbase", ToInt(rawIsBase, 0));
                            insertCommand.Parameters.AddWithValue("@linkingfield", (object)rawLinkingField ?? DBNull.Value);
                            insertCommand.Parameters.AddWithValue("@parenttable", (object)rawParentTable ?? DBNull.Value);
                            insertCommand.Parameters.AddWithValue("@incrementfield", (object)rawIncrementField ?? DBNull.Value);
                            insertCommand.Parameters.AddWithValue("@requireslink", ToInt(rawRequiresLink, 0));
                            insertCommand.Parameters.AddWithValue("@idconfig", (object)rawIdConfig ?? DBNull.Value);
                            insertCommand.Parameters.AddWithValue("@repeat_count_field", (object)rawRepeatCountField ?? DBNull.Value);
                            insertCommand.Parameters.AddWithValue("@auto_start_repeat", ToInt(rawAutoStartRepeat, 0));
                            insertCommand.Parameters.AddWithValue("@repeat_enforce_count", ToInt(rawRepeatEnforceCount, 0));
                            insertCommand.Parameters.AddWithValue("@display_fields", (object)rawDisplayFields ?? DBNull.Value);

                            insertCommand.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("ERROR: Could not add data to crfs table. " + ex.Message);
                logstring.Add("ERROR: Could not add data to crfs table. " + ex.Message);
            }
        }


        public void CopyMasterTables(string sourceDatabasePath, string db_path, string[] sourceTableNames)
        {
            try
            {
                // Check if the source database exists
                if (!File.Exists(sourceDatabasePath))
                {
                    // Exit the function if the master database doesn't exist
                    return;
                }

                // Connection strings for SQLite databases
                string sourceConnectionString = $"Data Source={sourceDatabasePath};Version=3;";
                string destConnectionString = $"Data Source={db_path};Version=3;";

                using (var sourceConnection = new SQLiteConnection(sourceConnectionString))
                {
                    sourceConnection.Open();

                    using (var destConnection = new SQLiteConnection(destConnectionString))
                    {
                        destConnection.Open();

                        foreach (string sourceTableName in sourceTableNames)
                        {
                            // Get the table schema from source
                            string getSchemaQuery = $"SELECT sql FROM sqlite_master WHERE type='table' AND name='{sourceTableName}'";
                            string createTableSql = "";

                            using (var schemaCommand = new SQLiteCommand(getSchemaQuery, sourceConnection))
                            {
                                var result = schemaCommand.ExecuteScalar();
                                if (result != null)
                                {
                                    createTableSql = result.ToString();
                                }
                            }

                            // Create the table in destination database
                            if (!string.IsNullOrEmpty(createTableSql))
                            {
                                using (var createCommand = new SQLiteCommand(createTableSql, destConnection))
                                {
                                    createCommand.ExecuteNonQuery();
                                }

                                // Copy data from source to destination
                                string selectQuery = $"SELECT * FROM {sourceTableName}";
                                using (var selectCommand = new SQLiteCommand(selectQuery, sourceConnection))
                                {
                                    using (var reader = selectCommand.ExecuteReader())
                                    {
                                        // Build INSERT command dynamically based on columns
                                        var columnCount = reader.FieldCount;
                                        var columnNames = new List<string>();
                                        var paramNames = new List<string>();

                                        for (int i = 0; i < columnCount; i++)
                                        {
                                            columnNames.Add(reader.GetName(i));
                                            paramNames.Add($"@param{i}");
                                        }

                                        string insertQuery = $"INSERT INTO {sourceTableName} ({string.Join(", ", columnNames)}) VALUES ({string.Join(", ", paramNames)})";

                                        while (reader.Read())
                                        {
                                            using (var insertCommand = new SQLiteCommand(insertQuery, destConnection))
                                            {
                                                for (int i = 0; i < columnCount; i++)
                                                {
                                                    insertCommand.Parameters.AddWithValue($"@param{i}", reader.GetValue(i));
                                                }
                                                insertCommand.ExecuteNonQuery();
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("ERROR: Could not copy master tables. " + ex.Message);
                logstring.Add("ERROR: Could not copy master tables. " + ex.Message);
            }
        }
    }
}