
using System;
using System.Collections.Generic;
using System.Text;
using ScrewTurn.Wiki.Plugins.OracleCommon;
using ScrewTurn.Wiki.PluginFramework;
using Oracle.DataAccess.Client;

namespace ScrewTurn.Wiki.Plugins.Oracle {

	/// <summary>
	/// Implements a Oracle-based files storage provider.
	/// </summary>
	public class OracleFilesStorageProvider : OracleFilesStorageProviderBase {

        private readonly ComponentInformation info = new ComponentInformation("Oracle Files Storage Provider", "Mr.Luo", "1.0.0.0", "https://github.com/swallowcamel/ScrewTurn.Wiki.Plugins.Oracle", "https://github.com/swallowcamel/ScrewTurn.Wiki.Plugins.Oracle");

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
            cmd.CommandText = "SELECT Version  FROM Version WHERE Component = 'Files'";

			bool exists = false;

			try {
				int version = ExecuteScalar<int>(cmd, -1);
				if(version > CurrentSchemaVersion) throw new InvalidConfigurationException("The version of the database schema is greater than the supported version");
				exists = version != -1;
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
            cmd.CommandText = "SELECT Version  FROM Version WHERE Component = 'Files'";

			bool exists = false;

			try {
				int version = ExecuteScalar<int>(cmd, -1);
				exists = version < CurrentSchemaVersion;
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
			
            ExecuteScript(cmd,Properties.Resources.FilesDatabase);
			
			cmd.Connection.Close();
		}

		/// <summary>
		/// Creates or updates the database schema if necessary.
		/// </summary>
		protected override void CreateOrUpdateDatabaseIfNecessary() {
			if(!SchemaExists()) {
				CreateStandardSchema();
			}
			if(SchemaNeedsUpdate()) {
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
			return host.GetProviderConfiguration(typeof(OracleSettingsStorageProvider).FullName);
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

	}

}
