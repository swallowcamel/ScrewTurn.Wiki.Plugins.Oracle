
using System;
using System.Collections.Generic;
using System.Text;
using ScrewTurn.Wiki.Plugins.OracleCommon;
using ScrewTurn.Wiki.PluginFramework;
using Oracle.DataAccess.Client;


namespace ScrewTurn.Wiki.Plugins.Oracle {
	
	/// <summary>
	/// Implements a Oracle-based users storage provider.
	/// </summary>
	public class OracleUsersStorageProvider : OracleUsersStorageProviderBase, IUsersStorageProviderV30 {

		private readonly ComponentInformation info = new ComponentInformation("Oracle Users Storage Provider", "Mr.Luo", "1.0.0.0", "http://www.screwturn.eu", "http://www.screwturn.eu/Version/OracleProv/Users.txt");
		
		private readonly OracleCommandBuilder commandBuilder = new OracleCommandBuilder();

		private const int CurrentSchemaVersion = 3000;

		/// <summary>
		/// Gets a new command with an open connection.
		/// </summary>
		/// <param name="connString">The connection string.</param>
		/// <returns>The command.</returns>
		private  OracleCommand GetCommand(string connString) {
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
			catch( OracleException ex) {
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
            cmd.CommandText = "SELECT Version  FROM Version WHERE Component = 'Users'";

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
            cmd.CommandText = "SELECT Version  FROM Version WHERE Component = 'Users'";

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

            ExecuteScript(cmd, Properties.Resources.UsersDatabase);

			cmd.Connection.Close();
		}

		/// <summary>
		/// Creates or updates the database schema if necessary.
		/// </summary>
		protected override void CreateOrUpdateDatabaseIfNecessary() {
			if(!SchemaExists()) {
				// Verify if an upgrade from version 2.0 is possible
				if(SchemaAllowsUpgradeFrom20()) {
					UpgradeFrom20();
				}
				else {
					// If not, create the standard schema
					CreateStandardSchema();
				}
			}
			if(SchemaNeedsUpdate()) {
				// Run minor update batches...
			}
		}

		/// <summary>
		/// Detects whether an upgrade is possible from version 2.0.
		/// </summary>
		/// <returns><c>true</c> if the upgrade is possible, <c>false</c> otherwise.</returns>
		private bool SchemaAllowsUpgradeFrom20() {
			// Look for 'UsersProviderVersion' tables
            OracleCommand cmd = GetCommand(connString);
            cmd.CommandText = "SELECT COUNT (*) FROM USER_TABLES WHERE upper(TABLE_NAME) = upper('PagesProviderVersion')";

            int count = ExecuteScalar<int>(cmd, -1);

            return count == 1;
		}

		/// <summary>
		/// Upgrades the database schema and data from version 2.0.
		/// </summary>
		private void UpgradeFrom20() {
			// 1. Load all user data in memory
			// 2. Rename old tables so they won't get in the way but the can still be recovered (_v2)
			// 3. Create new schema
			// 4. Add new users and default groups (admins, users)

			OracleCommand cmd = GetCommand(connString);
			cmd.CommandText = "select * from \"User\"";
            
			OracleDataReader reader = cmd.ExecuteReader();

			string administratorsGroup = host.GetSettingValue(SettingName.AdministratorsGroup);
			string usersGroup = host.GetSettingValue(SettingName.UsersGroup);

			List<UserInfo> newUsers = new List<UserInfo>(100);
			List<string> passwordHashes = new List<string>(100);

			while(reader.Read()) {
				string username = reader["Username"] as string;
				string passwordHash = reader["PasswordHash"] as string;
				string email = reader["Email"] as string;
				DateTime dateTime = (DateTime)reader["DateTime"];
				bool active = (bool)reader["Active"];
				bool admin = (bool)reader["Admin"];

				UserInfo temp = new UserInfo(username, null, email, active, dateTime, this);
				temp.Groups = admin ? new string[] { administratorsGroup } : new string[] { usersGroup };
				newUsers.Add(temp);
				passwordHashes.Add(passwordHash);
			}

			reader.Close();
			cmd.Connection.Close();

			cmd = GetCommand(connString);
			cmd.CommandText ="BEGIN RENAME UsersProviderVersion TO UsersProviderVersion_v2;  RENAME \"User\" TO User_v2; END;";
			cmd.ExecuteNonQuery();
			cmd.Connection.Close();

			CreateStandardSchema();

			UserGroup admins = AddUserGroup(administratorsGroup, "Built-in Administrators");
			UserGroup users = AddUserGroup(usersGroup, "Built-in Users");

			for(int i = 0; i < newUsers.Count; i++) {
				cmd = GetCommand(connString);
				cmd.CommandText = "insert into \"User\" (Username, PasswordHash, Email, Active, DateTime) values (:Username, :PasswordHash, :Email, :Active, :DateTime)";
				cmd.Parameters.Add(new OracleParameter(":Username", newUsers[i].Username));
				cmd.Parameters.Add(new OracleParameter(":PasswordHash", passwordHashes[i]));
				cmd.Parameters.Add(new OracleParameter(":Email", newUsers[i].Email));
				cmd.Parameters.Add(new OracleParameter(":Active", newUsers[i].Active?1:0));
				cmd.Parameters.Add(new OracleParameter(":DateTime", newUsers[i].DateTime));

				cmd.ExecuteNonQuery();
				cmd.Connection.Close();

				SetUserMembership(newUsers[i], newUsers[i].Groups);
			}

			host.UpgradeSecurityFlagsToGroupsAcl(admins, users);
		}

		/// <summary>
		/// Tries to load the configuration from a corresponding v2 provider.
		/// </summary>
		/// <returns>The configuration, or an empty string.</returns>
		protected override string TryLoadV2Configuration() {
			return host.GetProviderConfiguration("ScrewTurn.Wiki.PluginPack.OracleUsersStorageProvider");
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
