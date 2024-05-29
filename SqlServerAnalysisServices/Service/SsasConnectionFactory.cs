using Framework.Common;
using Framework.Model;
using Microsoft.AnalysisServices.AdomdClient;
using System.Data.Common;

namespace Framework.Service
{
    public class SsasConnectionFactory : ISsasConnectionFactory, ISsasConnectionFactoryConfigurator
    {
        public string ConnectionString => ConnectionStringBuilder.ConnectionString;

        protected DbConnectionStringBuilder ConnectionStringBuilder { get; set; } = new DbConnectionStringBuilder()
        {
            ["Provider"] = "MSOLAP",
            ["Persist Security Info"] = true,
            ["Integrated Security"] = "SSPI",
            ["Cube"] = "Model"
        };

        public virtual AdomdConnection Create(string connectionString) => new(connectionString);

        public virtual ISsasConnectionFactoryConfigurator UsePasswordEncryption(bool usePasswordEncryption)
        {
            ConnectionStringBuilder["Encrypt Password"] = usePasswordEncryption;
            return this;
        }

        public virtual ISsasConnectionFactoryConfigurator UsingConnectionString(string connectionString)
        {
            ConnectionStringBuilder.ConnectionString = connectionString;
            return this;
        }

        public virtual ISsasConnectionFactoryConfigurator WithCube(string cube)
        {
            ConnectionStringBuilder["Cube"] = cube;
            return this;
        }

        public virtual ISsasConnectionFactoryConfigurator WithDatabase(string database)
        {
            ConnectionStringBuilder["Catalog"] = database;
            return this;
        }

        public virtual ISsasConnectionFactoryConfigurator WithDataSource(string dataSource)
        {
            ConnectionStringBuilder["Data Source"] = dataSource ?? string.Empty;
            return this;
        }

        public virtual ISsasConnectionFactoryConfigurator WithEffectiveUserName(string effectiveUserName)
        {
            ConnectionStringBuilder["EffectiveUserName"] = effectiveUserName ?? string.Empty;
            return this;
        }

        public virtual ISsasConnectionFactoryConfigurator WithImpersonationLevel(ImpersonationLevel impersonation)
        {
            ConnectionStringBuilder["Impersonation Level"] = impersonation;
            return this;
        }
    }
}