using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeGeneration
{
    public class EnumGeneration
    {
        private readonly string _path;
        private const string FILE_NAME = "Enums_Generated.cs";

        public EnumGeneration(string relativePath)
        {
            _path = relativePath;
        }
   
        /// <summary>
        /// 
        /// </summary>
        /// <param name="serverName">ie: JP\SQLEXPRESS</param>
        /// <param name="dbName">ie: BreakAway</param>
        /// <param name="isEnumTable">delegate that determines whether the db table is considered a c# enum</param>
        /// <param name="columnNamesThatHoldStaticData">static table columns containing the static data. ie: 'StaticName'</param>
        public void GenerateEnums(string serverName, string dbName, Func<string,bool> isEnumTable, params string[] columnNamesThatHoldStaticData)
        {
            var server = new Server(serverName);
            var db = server.Databases[dbName];
            var csharp = new StringBuilder();
            string foundStaticColumn = "";
            string enumProp = "";
            var readerResultsPerTable = new List<string>(); 
            
            foreach (Table table in db.Tables)
            {
                if (isEnumTable(table.Name))
                {
                    readerResultsPerTable.Clear();
              
                    foreach (Column col in table.Columns)
                    {
                        foundStaticColumn = columnNamesThatHoldStaticData.FirstOrDefault(c => c.Equals(col.Name, StringComparison.CurrentCultureIgnoreCase));
                        if (foundStaticColumn != null)
                            break;
                    }

                    if (foundStaticColumn != null)
                    {
                        var ds = db.ExecuteWithResults(string.Format("select {0} from {1}", foundStaticColumn, table.Name));
                        var rdr = ds.CreateDataReader();

                        csharp.AppendLine("public Enum " + cleanName(table.Name));
                        csharp.AppendLine("{");

                        enumProp = "";

                        while (rdr.Read())
                        {
                            enumProp = rdr.GetAsDefaultIfDbNull<string>(0);
                            readerResultsPerTable.Add(enumProp);
                        }
                        rdr.Close();

                        var index = 0;
                        var countResult = readerResultsPerTable.Count;
                        foreach (var row in readerResultsPerTable)
                        {
                            var result = row;
                            if (index == 0)
                                result += string.Concat(row,"= 1");

                            if(index + 1 != countResult)
                            {
                                result += ",";
                            }

                            csharp.AppendLine(result);

                            index++;
                        }
                        
                        csharp.AppendLine("}");

                    }
                   
                }
            }

            if (csharp.Length > 0)
            {
                using (var writer = new StreamWriter(Path.Combine(_path, FILE_NAME), false))
                {
                    writer.Write(csharp.ToString());
                }
            }           
        }

        private string cleanName(string tblName)
        {
            return tblName.Trim().Replace(' ', '_').Replace('-', '_');
        }
    }

    public static class DbReaderExtensions
    {
        public static T GetAsDefaultIfDbNull<T>(this DbDataReader reader, int ordinal)
        {
            if (reader[ordinal] == DBNull.Value)
            {
                return default(T);
            }
            return (T)reader[ordinal];
        }
    }
}
