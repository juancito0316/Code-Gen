using System;
using System.Collections.Generic;
using System.Text;

namespace CodeGeneration.Configurations
{
    public class Configuration {
        public EnumConvention EnumConvention { get; set; }
        public StoredProcedureConvention StoredProcedureConvention { get; set; }
        public ClassConvention ClassConvention { get; set; }
    }

    public class EnumConvention {
        public string ColumnName { get; set; }
        public string[] TargetTables { get; set; }
    }

    public class StoredProcedureConvention {

    }

    public class ClassConvention {

    }
}
