using Microsoft.SqlServer.Management.Smo;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeGeneration
{
    public class SprocGeneration
    {
        private readonly string _path;

        public SprocGeneration(string relativePath)
        {
            _path = relativePath;
        }

        //TODO: implement useTrans
        public void GenerateSaves(string serverName, string dbName, Func<string,bool> excludedColumn)
        {
            var server = new Server(serverName);
            var db = server.Databases[dbName];
            var csharp = new StringBuilder();

            foreach (Table table in db.Tables)
            {
                var tableName = table.Name;
                var tableColumns = new List<Column>();
                var countColumns = 0;
                Column identityCol = null;
                int index = 0;

                csharp.Clear();
                csharp.AppendLine(string.Format("create procedure {0}", tableName+ "_Save"));
          
                foreach (Column col in table.Columns)
                {
                    if (!col.DataType.Name.Equals("timestamp", StringComparison.CurrentCultureIgnoreCase) && !excludedColumn(col.Name))
                    {
                        tableColumns.Add(col);
                    }           
                }

                countColumns = tableColumns.Count;
                //parameters     
                foreach (Column col in tableColumns)
                {
                    if (!excludedColumn(col.Name))
                    {
                        if (col.Identity)
                            identityCol = col;

                        var isNullable = col.Nullable;
                        string nullableOrEmpty = isNullable == true ? " = null" : "";
                        nullableOrEmpty += ",";

                        if (identityCol == null && index == countColumns - 1)
                            nullableOrEmpty = nullableOrEmpty.Remove(nullableOrEmpty.Length - 1);

                        string parameterLine = string.Format("@{0} {1} {2}", col.Name, col.DataType.Name, nullableOrEmpty);

                        csharp.AppendLine(parameterLine);

                        index++;
                    }

                }

                if (identityCol == null)
                {
                    HandleNoIdentity(csharp,tableName, tableColumns, excludedColumn);
                }
                else
                {
                   HandleWithIdentity(csharp, tableName, identityCol,tableColumns, excludedColumn);
                }
                
                if (csharp.Length > 0)
                {
                    string sprocName = string.Concat(tableName, "_Save.sql");
                    using (var writer = new StreamWriter(Path.Combine(_path, sprocName), false))
                    {
                        writer.Write(csharp.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// creates a simple upsert. No output pkey
        /// </summary>
        /// <param name="csharp"></param>
        /// <param name="tableName"></param>
        /// <param name="tableColumns"></param>
        private void HandleNoIdentity(StringBuilder csharp, string tableName, IEnumerable<Column> tableColumns, Func<string, bool> excludedColumn)
        {
            
            csharp.AppendLine("as");
            csharp.AppendLine("begin");
            csharp.AppendLine(string.Format("insert into {0}", tableName));
            csharp.AppendLine("(");

            //insert into
            var index = 0;
            var countColumns = tableColumns.Count();
            foreach (Column col in tableColumns)
            {
                if (!excludedColumn(col.Name))
                {
                    string columnInsertInto = col.Name;
                    if (ShouldAppendComma(index, countColumns))
                        columnInsertInto += ",";

                    csharp.AppendLine(columnInsertInto);
                    index++;
                }               
            }
            csharp.AppendLine(")");
            csharp.AppendLine("values");
            csharp.AppendLine("(");

            //values
            index = 0;
            foreach (Column col in tableColumns)
            {
                if (!excludedColumn(col.Name))
                {
                    string columnInsertInto = string.Concat("@", col.Name);
                    if (ShouldAppendComma(index, countColumns))
                        columnInsertInto += ",";

                    csharp.AppendLine(columnInsertInto);
                    index++;
                }                          
            }
            csharp.AppendLine(")");
            csharp.AppendLine("end");
        }

        /// <summary>
        /// upserts the table and returns primary key as output parameter
        /// </summary>
        /// <param name="csharp"></param>
        /// <param name="tableName"></param>
        /// <param name="identityCol"></param>
        /// <param name="tableColumns"></param>
        private void HandleWithIdentity(StringBuilder csharp, string tableName, Column identityCol, IEnumerable<Column> tableColumns, Func<string,bool> excludedColumn)
        {
             int index = 0;
             int countColumns = tableColumns.Count();

            csharp.AppendLine(string.Format("@identity {0} output", identityCol.DataType.Name));

            csharp.AppendLine("as");
            csharp.AppendLine("begin");

            //update
            csharp.AppendLine(string.Format("if exists(select * from {0} with(updlock) where {1} = @{2})", tableName, identityCol.Name,identityCol.Name));
            csharp.AppendLine("begin");
            csharp.AppendLine(string.Concat("update ",tableName));
            csharp.AppendLine("set");

            foreach(Column col in tableColumns){
                if (col.Identity && col.IdentityIncrement > 0)
                    continue;

                if (!excludedColumn(col.Name))
                {
                    string commaIfApplicable = "";
                    if (ShouldAppendComma(index, countColumns - 1)) //countColumns-1 because the identity column is not part of the update batch
                        commaIfApplicable += ",";

                    csharp.AppendLine(string.Concat(col.Name, " = ", "@", col.Name, commaIfApplicable));

                    index++;
                }
            }

            csharp.AppendLine(string.Format("where {0} = @{1}", identityCol.Name, identityCol.Name));
            csharp.AppendLine(string.Concat("set @identity = @", identityCol.Name));
            csharp.AppendLine("end");
            //end update

            csharp.AppendLine("else");
            csharp.AppendLine("begin");

            //insert
            csharp.AppendLine(string.Format("insert into {0}", tableName));
            csharp.AppendLine("(");

             index = 0;
             foreach (Column col in tableColumns)
             {
                 if (col.Identity && col.IdentityIncrement > 0)
                     continue;

                 if (!excludedColumn(col.Name))
                 {
                     string columnInsertInto = col.Name;
                     if (ShouldAppendComma(index, countColumns - 1))
                         columnInsertInto += ",";

                     csharp.AppendLine(columnInsertInto);
                     index++;
                 }               
             }
             csharp.AppendLine(")");
             csharp.AppendLine("values");
             csharp.AppendLine("(");

             index = 0;
             foreach (Column col in tableColumns)
             {
                 if (col.Identity && col.IdentityIncrement > 0)
                     continue;

                 if (!excludedColumn(col.Name))
                 {
                     string columnInsertInto = string.Concat("@", col.Name);
                     if (ShouldAppendComma(index, countColumns - 1))
                         columnInsertInto += ",";

                     csharp.AppendLine(columnInsertInto);
                     index++;
                 }               

             }
             csharp.AppendLine(")");
             csharp.AppendLine("set @identity = scope_identity()");
             csharp.AppendLine("end");
             csharp.AppendLine("return @identity");
             csharp.AppendLine("end");
        }

        private bool ShouldAppendComma(int iter, int countCol)
        {
            return iter < countCol - 1;
        }
    
    }

}
