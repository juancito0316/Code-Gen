using CodeGeneration.Constructs;
using CodeGeneration.Interfaces;
using Microsoft.SqlServer.Management.Smo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodeGeneration.Strategies
{
    public class GenerationViaStringBuilderStrategy : IGenerationStrategy {

        readonly StringBuilder _syntaxBuilder;
        readonly Server _server;
        readonly Database _db;
        readonly Construct _construct;

        public GenerationViaStringBuilderStrategy(Construct construct, Server server, Database db) {
            _syntaxBuilder = new StringBuilder();
            _server = server;
            _db = db;
            _construct = construct;
        }

        public byte[] Execute(Configurations.Configuration config) {

            //todo: refacator into classes.
            switch (_construct) {
                case Construct.Enumeration:
                    return GenerateEnums(config);
                default:
                    return null;
            }
           
        }

        private byte[] GenerateEnums(Configurations.Configuration config) {

            var enumConfiguration = config.EnumConvention;
            var foundColumnWithEnumValues = false;
            var enumValues = new List<string>();
            var wereTableNamesProvided = enumConfiguration.TargetTables.Length > 0;
            TableCollection tablesToScan = null;

            //if table names were provided, we'll only iterate over those, otherwise, operate on all tables
            if (wereTableNamesProvided) {
                foreach(Table table in _db.Tables) {
                    if (enumConfiguration.TargetTables.Contains(table.Name)) {
                        tablesToScan.Add(table);
                    }
                }
            } else {
                tablesToScan = _db.Tables;
            }

            //Loop through db tables
            foreach (Table table in tablesToScan) {
                
                //reset the enumValues to be used for current table iteration
                enumValues.Clear();

                foreach (Column column in table.Columns) {
                    foundColumnWithEnumValues = enumConfiguration.ColumnName.Equals(column.Name);
                    if (foundColumnWithEnumValues) {
                        break;
                    }
                }

                if (!foundColumnWithEnumValues) {
                    continue;
                }

                var query = string.Format("select {0} from {1}", enumConfiguration.ColumnName, table.Name);
                var dataset = _db.ExecuteWithResults(query);
                var dataReader = dataset.CreateDataReader();

                _syntaxBuilder.AppendLine("public enum " + CleanName(table.Name));
                _syntaxBuilder.AppendLine("{");

                while (dataReader.Read()) {
                    var enumValue = dataReader.GetAsDefaultIfDbNull<string>(0);
                    enumValues.Add(enumValue);
                }

                dataReader.Close();

                var rowIndex = 0;
                var countTotalRowsInTable = enumValues.Count;
                var valueNormalizedForEnum = "";
                foreach (var value in enumValues) {
                    
                    if (rowIndex == 0) {
                        valueNormalizedForEnum += string.Concat(value, "= 1,");
                    }

                    //only append the , when applicable
                    if(rowIndex + 1 < countTotalRowsInTable) {
                        valueNormalizedForEnum += ",";
                    }

                    _syntaxBuilder.AppendLine(valueNormalizedForEnum);

                    rowIndex++;
                }
            }

            if(_syntaxBuilder.Length == 0) {
                return null;
            }
            
            return Encoding.ASCII.GetBytes(_syntaxBuilder.ToString());
        }

        string CleanName(string name) {
            return name.Trim().Replace(' ', '_').Replace('-', '_');
        }

    }
}
