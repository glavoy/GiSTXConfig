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
                        CREATE TABLE IF NOT EXISTS crfs (
	                        tablename	TEXT,
	                        primarykey	TEXT,
	                        displayname	TEXT,
	                        isbase	INTEGER DEFAULT 0,
	                        linkingfield	TEXT,
	                        parenttable	TEXT,
	                        incrementfield	TEXT,
	                        requireslink	INTEGER DEFAULT 0,
	                        idconfig	TEXT
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
                        "INSERT INTO crfs (tablename, primarykey, displayname, isbase, linkingfield, parenttable, incrementfield, requireslink, idconfig) " +
                        "VALUES (@tablename, @primarykey, @displayname, @isbase, @linkingfield, @parenttable, @incrementfield, @requireslink, @idconfig)";

                    using (var insertCommand = new SQLiteCommand(insertQuery, connection))
                    {
                        Excel.Range usedRange = crf_ws.UsedRange;
                        for (int row = 2; row <= usedRange.Rows.Count; row++)
                        {
                            insertCommand.Parameters.Clear();

                            insertCommand.Parameters.AddWithValue("@tablename", ((Excel.Range)usedRange.Cells[row, 1]).Value2);
                            insertCommand.Parameters.AddWithValue("@primarykey", ((Excel.Range)usedRange.Cells[row, 2]).Value2);
                            insertCommand.Parameters.AddWithValue("@displayname", ((Excel.Range)usedRange.Cells[row, 3]).Value2);
                            insertCommand.Parameters.AddWithValue("@isbase", ((Excel.Range)usedRange.Cells[row, 4]).Value2 ?? 0);
                            insertCommand.Parameters.AddWithValue("@linkingfield", ((Excel.Range)usedRange.Cells[row, 5]).Value2);
                            insertCommand.Parameters.AddWithValue("@parenttable", ((Excel.Range)usedRange.Cells[row, 6]).Value2);
                            insertCommand.Parameters.AddWithValue("@incrementfield", ((Excel.Range)usedRange.Cells[row, 7]).Value2);
                            insertCommand.Parameters.AddWithValue("@requireslink", ((Excel.Range)usedRange.Cells[row, 8]).Value2 ?? 0);
                            insertCommand.Parameters.AddWithValue("@idconfig", ((Excel.Range)usedRange.Cells[row, 9]).Value2);

                            insertCommand.ExecuteNonQuery();
                        }
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