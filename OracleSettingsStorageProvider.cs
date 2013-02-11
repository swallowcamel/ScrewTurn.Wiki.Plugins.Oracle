
using System;
using System.Collections.Generic;
using System.Text;
using ScrewTurn.Wiki.Plugins.OracleCommon;
using ScrewTurn.Wiki.PluginFramework;
using Oracle.DataAccess.Client;
using System.Xml;
using System.Data;

namespace ScrewTurn.Wiki.Plugins.Oracle {

	/// <summary>
	/// Implements a Oracle-based settings storage provider.
	/// </summary>
	public class OracleSettingsStorageProvider : OracleSettingsStorageProviderBase {

        private readonly ComponentInformation info = new ComponentInformation("Oracle Settings Storage Provider", "Mr.Luo", "1.0.0.0", "https://github.com/swallowcamel/ScrewTurn.Wiki.Plugins.Oracle", "https://github.com/swallowcamel/ScrewTurn.Wiki.Plugins.Oracle");
		
		private readonly OracleCommandBuilder commandBuilder = new OracleCommandBuilder();

		private const int CurrentSchemaVersion = 3000;

		/// <summary>
		/// Gets a new command with an open connection.
		/// </summary>
		/// <param name="connString">The connection string.</param>
		/// <returns>The command.</returns>
		private OracleCommand GetCommand(string connString) {
            return commandBuilder.GetCommand(connString, "SELECT SCHEMANAME  FROM V$SESSION WHERE STATUS = 'ACTIVE' AND TYPE = 'USER'", new List<Parameter>()) as OracleCommand;
		}

		/// <summary>
		/// Gets a new command builder object.
		/// </summary>
		/// <returns>The command builder.</returns>
		protected override ICommandBuilder GetCommandBuilder() {
			return commandBuilder;
		}

		/// <summary>
		/// Validates a connection string.
		/// </summary>
		/// <param name="connString">The connection string to validate.</param>
		/// <remarks>If the connection string is invalid, the method throws <see cref="T:InvalidConfigurationException"/>.</remarks>
		protected override void ValidateConnectionString(string connString) {
			OracleCommand cmd = null;
			try {
				cmd = GetCommand(connString);
			}
			catch(OracleException ex) {
				throw new InvalidConfigurationException("Provided connection string is not valid", ex);
			}
			catch(InvalidOperationException ex) {
				throw new InvalidConfigurationException("Provided connection string is not valid", ex);
			}
			catch(ArgumentException ex) {
				throw new InvalidConfigurationException("Provided connection string is not valid", ex);
			}
			finally {
				try {
					cmd.Connection.Close();
				}
				catch { }
			}
		}

		/// <summary>
		/// Detects whether the database schema exists.
		/// </summary>
		/// <returns><c>true</c> if the schema exists, <c>false</c> otherwise.</returns>
		private bool SchemaExists() {
			OracleCommand cmd = GetCommand(connString);
			cmd.CommandText = "SELECT Version  FROM Version WHERE Component = 'Settings'";

			bool exists = false;

			try {
				int VERSION = ExecuteScalar<int>(cmd, -1);
				if(VERSION > CurrentSchemaVersion) throw new InvalidConfigurationException("The Version of the database schema is greater than the supported Version");
				exists = VERSION != -1;
			}
			catch(OracleException) {
				exists = false;
			}
			finally {
				try {
					cmd.Connection.Close();
				}
				catch { }
			}

			return exists;
		}

		/// <summary>
		/// Detects whether the database schema needs to be updated.
		/// </summary>
		/// <returns><c>true</c> if an update is needed, <c>false</c> otherwise.</returns>
		private bool SchemaNeedsUpdate() {
			OracleCommand cmd = GetCommand(connString);
			cmd.CommandText = "SELECT Version  FROM Version WHERE Component = 'Settings'";

			bool exists = false;

			try {
				int VERSION = ExecuteScalar<int>(cmd, -1);
				exists = VERSION < CurrentSchemaVersion;
			}
			catch(OracleException) {
				exists = false;
			}
			finally {
				try {
					cmd.Connection.Close();
				}
				catch { }
			}

			return exists;
		}

		/// <summary>
		/// Creates the standard database schema.
		/// </summary>
		private void CreateStandardSchema() {
			OracleCommand cmd = GetCommand(connString);

            ExecuteScript(cmd, Properties.Resources.SettingsDatabase);

            cmd.Connection.Close();
        }

		/// <summary>
		/// Creates or updates the database schema if necessary.
		/// </summary>
		protected override void CreateOrUpdateDatabaseIfNecessary() {
			if(!SchemaExists()) {
				CreateStandardSchema();
				isFirstStart = true;
			}else if(SchemaNeedsUpdate()) {
				// Run minor update batches...
			}
		}

		/// <summary>
		/// Tries to load the configuration from a corresponding v2 provider.
		/// </summary>
		/// <returns>The configuration, or an empty string.</returns>
		protected override string TryLoadV2Configuration() {
			return "";
		}

		/// <summary>
		/// Tries to load the configuration of the corresponding settings storage provider.
		/// </summary>
		/// <returns>The configuration, or an empty string.</returns>
		protected override string TryLoadSettingsStorageProviderConfiguration() {
			return "";
		}

		/// <summary>
		/// Gets the Information about the Provider.
		/// </summary>
		public override ComponentInformation Information {
			get { return info; }
		}

		/// <summary>
		/// Gets a brief summary of the configuration string format, in HTML. Returns <c>null</c> if no configuration is needed.
		/// </summary>
		public override string ConfigHelpHtml {
            get { return "Connection string format:<br /><code>Data Source=<i>ip:port/instance</i>;User ID=<i>login</i>;Password=<i>password</i>;</code>"; }
		}

		/// <summary>
		/// Gets the default users storage provider, when no value is stored in the database.
		/// </summary>
		protected override string DefaultUsersStorageProvider {
			get { return typeof(OracleUsersStorageProvider).FullName; }
		}

		/// <summary>
		/// Gets the default pages storage provider, when no value is stored in the database.
		/// </summary>
		protected override string DefaultPagesStorageProvider {
			get { return typeof(OraclePagesStorageProvider).FullName; }
		}

		/// <summary>
		/// Gets the default files storage provider, when no value is stored in the database.
		/// </summary>
		protected override string DefaultFilesStorageProvider {
			get { return typeof(OracleFilesStorageProvider).FullName; }
		}

	}

}
