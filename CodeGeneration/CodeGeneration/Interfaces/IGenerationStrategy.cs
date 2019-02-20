using CodeGeneration.Constructs;
using Microsoft.SqlServer.Management.Smo;
using System;
using System.Collections.Generic;
using System.Text;

namespace CodeGeneration.Interfaces
{
    public interface IGenerationStrategy {
        byte[] Execute(Configurations.Configuration config);
    }
}
