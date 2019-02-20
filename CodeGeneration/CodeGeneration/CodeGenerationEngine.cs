using CodeGeneration.Constructs;
using CodeGeneration.Interfaces;
using Microsoft.SqlServer.Management.Smo;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CodeGeneration
{
    public sealed class CodeGenerationEngine {

        readonly string _targetGenerationPath;
        readonly Server _server;
        readonly Database _db;

        public CodeGenerationEngine(string serverName, string dbName, string targetGenerationPath) {
            _targetGenerationPath = targetGenerationPath;
            _server = new Server(serverName);
            _db = _server.Databases[dbName];
        }

        public void Generate(Construct construct, IGenerationStrategy strategy) {

            var configuration = ObtainConfiguration();
            var results = strategy.Execute(configuration);

            if (results.Length == 0)
                return;

            using(var writer = new StreamWriter(Path.Combine(_targetGenerationPath, "generated"), false)) {
                var content = System.Text.Encoding.UTF8.GetString(results);
                writer.Write(content);
                writer.Flush();
            }
        }

        private Configurations.Configuration ObtainConfiguration() {
            var configurationFilePath = Path.Combine(Directory.GetCurrentDirectory(), "\\configuration.json");
            try {
                return JsonConvert.DeserializeObject<Configurations.Configuration>(File.ReadAllText(configurationFilePath));
            } catch (Exception ex) {
                throw;
            }
        }

    }
}
