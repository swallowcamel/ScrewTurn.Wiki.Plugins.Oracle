
using System;
using System.Collections.Generic;
using System.Text;
using ScrewTurn.Wiki.PluginFramework;
using ScrewTurn.Wiki.Plugins.OracleCommon;
using Oracle.DataAccess.Client;
using System.Xml;
using System.Data;

namespace ScrewTurn.Wiki.Plugins.Oracle {

	/// <summary>
	/// Implements a Oracle-based users storage provider.
	/// </summary>
	public class OraclePagesStorageProvider : OraclePagesStorageProviderBase, IPagesStorageProviderV30 {

        private readonly ComponentInformation info = new ComponentInformation("Oracle Pages Storage Provider", "Mr.Luo", "1.0.0.0", "http://www.screwturn.eu", "http://www.screwturn.eu/Version/OracleProv/Users.txt");

		private readonly OracleCommandBuilder commandBuilder = new OracleCommandBuilder();

		private const int CurrentSchemaVersion = 3001;

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
            cmd.CommandText = "SELECT Version  FROM Version WHERE Component = 'Pages'";

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
            cmd.CommandText = "SELECT Version  FROM Version WHERE Component = 'Pages'";

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
                       
            ExecuteScript(cmd,Properties.Resources.PagesDatabase);
           
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
				Update3000to3001();
				// Other update batches
			}
		}

		/// <summary>
		/// Updates the database schema from version 3000 to version 3001.
		/// </summary>
		private void Update3000to3001() {
			OracleCommand cmd = GetCommand(connString);

            ExecuteScript(cmd, Properties.Resources.PagesDatabase_3000to3001);

			cmd.Connection.Close();
		}

		/// <summary>
		/// Detects whether an upgrade is possible from version 2.0.
		/// </summary>
		/// <returns><c>true</c> if the upgrade is possible, <c>false</c> otherwise.</returns>
		private bool SchemaAllowsUpgradeFrom20() {
			// Look for 'PagesProviderVersion' table
			OracleCommand cmd = GetCommand(connString);
            cmd.CommandText = "SELECT COUNT (*) FROM USER_TABLES WHERE upper(TABLE_NAME) = upper('PagesProviderVersion')";

			int count = ExecuteScalar<int>(cmd, -1);

			return count == 1;
		}

		/// <summary>
		/// Upgrades the database schema and data from version 2.0.
		/// </summary>
		private void UpgradeFrom20() {
			// Procedure
			// 1. Rename old tables (_v2) and create new schema
			// 2. Copy snippets
			// 3. Copy pages
			// 4. Copy messages
			// 5. Copy categories
			// 6. Copy category bindings
			// 7. Copy nav paths
			// 8. Rename offending pages and categories (names with dots) leveraging cascaded FKs
			// 9. Update security to use ACL

			OracleCommand cmd = GetCommand(connString);
            cmd.CommandText =
@"
BEGIN
    RENAME Page                   TO Page_v2;
    RENAME PageContent            TO PageContent_v2;
    RENAME Category               TO Category_v2;
    RENAME CategoryBinding        TO CategoryBinding_v2;
    RENAME Message                TO Message_v2;
    RENAME Snippet                TO Snippet_v2;
    RENAME NavigationPath         TO NavigationPath_v2;
    RENAME NavigationPathBinding  TO NavigationPathBinding_v2;
    RENAME PagesProviderVersion   TO PagesProviderVersion_v2;
 END;";

			cmd.ExecuteNonQuery();
			cmd.Connection.Close();

			CreateStandardSchema();

			cmd = GetCommand(connString);
            cmd.CommandText = "CREATE TABLE Snippet AS SELECT Name,Content FROM Snippet_v2";
			cmd.ExecuteNonQuery();

			Dictionary<string, char> pageStatus = new Dictionary<string, char>(500);

			cmd.CommandText = "select * from Page_v2";
			using(OracleDataReader reader = cmd.ExecuteReader()) {
				OracleCommand insertCmd = GetCommand(connString);
				insertCmd.CommandText = "insert into Page (Name, Namespace, CreationDateTime) values (:Name, ' ', :CreationDateTime)";

				while(reader.Read()) {
					insertCmd.Parameters.Clear();
					insertCmd.Parameters.Add(new OracleParameter(":Name", reader["Name"] as string));
					insertCmd.Parameters.Add(new OracleParameter(":CreationDateTime", (DateTime)reader["CreationDateTime"]));
					pageStatus.Add(reader["Name"] as string, (reader["Status"] as string).ToUpperInvariant()[0]);

					insertCmd.ExecuteNonQuery();
				}

				insertCmd.Connection.Close();
			}

			cmd.CommandText = "select * from PageContent_v2";
			using(OracleDataReader reader = cmd.ExecuteReader()) {
				OracleCommand insertCmd = GetCommand(connString);
				insertCmd.CommandText = "insert into PageContent (Page, Namespace, Revision, Title, User, LastModified, Comment, Content, Description) values (:Page, ' ', :Revision, :Title, :User, :LastModified, :Comment, :Content, NULL)";

				while(reader.Read()) {
					insertCmd.Parameters.Clear();
					insertCmd.Parameters.Add(new OracleParameter(":Page", reader["Page"] as string));
					insertCmd.Parameters.Add(new OracleParameter(":Revision", (short)(int)Convert.ChangeType(reader["Revision"],typeof(int))));
					insertCmd.Parameters.Add(new OracleParameter(":Title", reader["Title"] as string));
					insertCmd.Parameters.Add(new OracleParameter(":User", reader["Username"] as string));
					insertCmd.Parameters.Add(new OracleParameter(":LastModified", (DateTime)reader["DateTime"]));
					insertCmd.Parameters.Add(new OracleParameter(":Comment", reader["Comment"] as string)); // Cannot be null in v2
					insertCmd.Parameters.Add(new OracleParameter(":Content", reader["Content"] as string));

					insertCmd.ExecuteNonQuery();
				}

				insertCmd.Connection.Close();
			}

			cmd.CommandText = "select * from Message_v2";
			using(OracleDataReader reader = cmd.ExecuteReader()) {
				OracleCommand insertCmd = GetCommand(connString);
				insertCmd.CommandText = "insert into Message (Page, Namespace, Id, Parent, Username, Subject, DateTime, Body) values (:Page, ' ', :Id, :Parent, :Username, :Subject, :DateTime, :Body)";

				while(reader.Read()) {
					insertCmd.Parameters.Clear();
					insertCmd.Parameters.Add(new OracleParameter(":Page", reader["Page"] as string));
					insertCmd.Parameters.Add(new OracleParameter(":Id", (short)(int)Convert.ChangeType(reader["ID"],typeof(int))));
					int parent = (int)Convert.ChangeType(reader["Parent"],typeof(int));
					if(parent == -1) insertCmd.Parameters.Add(new OracleParameter(":Parent", DBNull.Value));
					else insertCmd.Parameters.Add(new OracleParameter(":Parent", (short)parent));
					insertCmd.Parameters.Add(new OracleParameter(":Username", reader["Username"] as string));
					insertCmd.Parameters.Add(new OracleParameter(":Subject", reader["Subject"] as string));
					insertCmd.Parameters.Add(new OracleParameter(":DateTime", (DateTime)reader["DateTime"]));
					insertCmd.Parameters.Add(new OracleParameter(":Body", reader["Body"] as string));

					insertCmd.ExecuteNonQuery();
				}

				insertCmd.Connection.Close();
			}

			cmd.CommandText = "select * from Category_v2";
			using(OracleDataReader reader = cmd.ExecuteReader()) {
				OracleCommand insertCmd = GetCommand(connString);
				insertCmd.CommandText = "insert into Category (Name, Namespace) values (:Name, ' ')";

				while(reader.Read()) {
					insertCmd.Parameters.Clear();
					insertCmd.Parameters.Add(new OracleParameter(":Name", reader["Name"] as string));

					insertCmd.ExecuteNonQuery();
				}

				insertCmd.Connection.Close();
			}

			cmd.CommandText = "select * from CategoryBinding_v2";
			using(OracleDataReader reader = cmd.ExecuteReader()) {
				OracleCommand insertCmd = GetCommand(connString);
				insertCmd.CommandText = "insert into CategoryBinding (Namespace, Category, Page) values (' ', :Category, :Page)";

				while(reader.Read()) {
					insertCmd.Parameters.Clear();
					insertCmd.Parameters.Add(new OracleParameter(":Category", reader["Category"] as string));
					insertCmd.Parameters.Add(new OracleParameter(":Page", reader["Page"] as string));

					insertCmd.ExecuteNonQuery();
				}

				insertCmd.Connection.Close();
			}

			cmd.CommandText = "select * from NavigationPathBinding_v2";
			using(OracleDataReader reader = cmd.ExecuteReader()) {
				OracleCommand insertCmd = GetCommand(connString);
				insertCmd.CommandText = "insert into NavigationPath (Name, Namespace, Page, Number) values (:Name, ' ', :Page, :Number)";

				while(reader.Read()) {
					insertCmd.Parameters.Clear();
					insertCmd.Parameters.Add(new OracleParameter(":Name", reader["NavigationPath"] as string));
					insertCmd.Parameters.Add(new OracleParameter(":Page", reader["Page"] as string));
					insertCmd.Parameters.Add(new OracleParameter(":Number", (short)(int)Convert.ChangeType(reader["Number"],typeof(int))));

					insertCmd.ExecuteNonQuery();
				}

				insertCmd.Connection.Close();
			}

			// Rename pages and categories
			List<string> allPages = new List<string>(500);
			List<string> allCategories = new List<string>(50);

			cmd.CommandText = "select Name from Page";
			using(OracleDataReader reader = cmd.ExecuteReader()) {
				while(reader.Read()) {
					allPages.Add(reader["Name"] as string);
				}
			}

			cmd.CommandText = "select Name from Category";
			using(OracleDataReader reader = cmd.ExecuteReader()) {
				while(reader.Read()) {
					allCategories.Add(reader["Name"] as string);
				}
			}

            cmd.CommandText =
@"
BEGIN
for c in (select 'ALTER TABLE '||TABLE_NAME||' DISABLE CONSTRAINT '||constraint_name||' ' as v_sql from user_constraints where upper(TABLE_NAME)=upper('CategoryBinding') and CONSTRAINT_TYPE='R') loop
DBMS_OUTPUT.PUT_LINE(C.V_SQL);
begin
 EXECUTE IMMEDIATE c.v_sql;
 exception when others then
 dbms_output.put_line(sqlerrm);
 end;
end loop; 
for c in (select 'ALTER TABLE '||TNAME||' DISABLE ALL TRIGGERS ' AS v_sql from tab where upper(TNAME)=upper('CategoryBinding') and tabtype='TABLE') loop
 dbms_output.put_line(c.v_sql);
 begin
 execute immediate c.v_sql;
exception when others then
 dbms_output.put_line(sqlerrm);
 end;
end loop;
end;";
			cmd.ExecuteNonQuery();

            cmd.CommandText =
@"
BEGIN
   UPDATE Page
      SET Name = :NewName
    WHERE Name = :OldName;

   UPDATE CategoryBinding
      SET Page = :NewName2
    WHERE Page = :OldName2;
END;";

			foreach(string page in allPages) {
				if(page.Contains(".")) {
					cmd.Parameters.Clear();
					cmd.Parameters.Add(new OracleParameter(":NewName", page.Replace(".", "_")));
					cmd.Parameters.Add(new OracleParameter(":OldName", page));
					cmd.Parameters.Add(new OracleParameter(":NewName2", page.Replace(".", "_")));
					cmd.Parameters.Add(new OracleParameter(":OldName2", page));

					cmd.ExecuteNonQuery();
				}
			}

            cmd.CommandText = @"
BEGIN
for c in (select 'ALTER TABLE '||TABLE_NAME||' ENABLE CONSTRAINT '||constraint_name||' ' as v_sql from user_constraints where upper(TABLE_NAME)=upper('CategoryBinding') and CONSTRAINT_TYPE='R') loop
DBMS_OUTPUT.PUT_LINE(C.V_SQL);
begin
 EXECUTE IMMEDIATE c.v_sql;
 exception when others then
 dbms_output.put_line(sqlerrm);
 end;
end loop; 
for c in (select 'ALTER TABLE '||TNAME||' ENABLE ALL TRIGGERS ' AS v_sql from tab where upper(TNAME)=upper('CategoryBinding') and tabtype='TABLE') loop
 dbms_output.put_line(c.v_sql);
 begin
 execute immediate c.v_sql;
exception when others then
 dbms_output.put_line(sqlerrm);
 end;
end loop;
end;";

			cmd.ExecuteNonQuery();

			cmd.CommandText = "update Category set Name = :NewName where Name = :OldName";
			foreach(string category in allCategories) {
				if(category.Contains(".")) {
					cmd.Parameters.Clear();
					cmd.Parameters.Add(new OracleParameter(":NewName", category.Replace(".", "_")));
					cmd.Parameters.Add(new OracleParameter(":OldName", category));

					cmd.ExecuteNonQuery();
				}
			}

			cmd.Connection.Close();

			string[] keys = new string[pageStatus.Count];
			pageStatus.Keys.CopyTo(keys, 0);
			foreach(string key in keys) {
				if(key.Contains(".")) {
					char status = pageStatus[key];
					pageStatus.Remove(key);
					pageStatus.Add(key.Replace(".", "_"), status);
				}
			}

			// Setup permissions for single pages
			foreach(KeyValuePair<string, char> pair in pageStatus) {
				if(pair.Value != 'N') {
					// Need to set permissions emulating old-style behavior
					host.UpgradePageStatusToAcl(new PageInfo(pair.Key, this, DateTime.MinValue), pair.Value);
				}
			}
		}

		/// <summary>
		/// Tries to load the configuration from a corresponding v2 provider.
		/// </summary>
		/// <returns>The configuration, or an empty string.</returns>
		protected override string TryLoadV2Configuration() {
			return host.GetProviderConfiguration("ScrewTurn.Wiki.PluginPack.OraclePagesStorageProvider");
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
