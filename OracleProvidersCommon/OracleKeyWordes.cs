﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScrewTurn.Wiki.Plugins.OracleCommon
{
    public class OracleKeyWords
    {
        static List<string> oracleKeyWord = new List<string> { 
            "ACCESS","ADD","ALL","ALTER","AND","ANY","AS","ASC","AUDIT","BETWEEN","BY","CHAR","CHECK","CLUSTER","COLUMN","COLUMN_AUTH_INDICATOR",
            "COLUMN_VALUE","COMMENT","COMPRESS","CONNECT","CONSTRAINTS","CREATE","CURRENT","DATE","DECIMAL","DEFAULT","DELETE","DESC","DISTINCT",
            "DROP","ELIMINATE_OUTER_JOIN","ELSE","EXCLUSIVE","EXISTS","FILE","FLOAT","FOR","FROM","GRANT","GROUP","HAVING","IDENTIFIED","IMMEDIATE",
            "IN","INCREMENT","INDEX","INDEX_RS","INITIAL","INSERT","INTEGER","INTERSECT","INTO","IS","LEVEL","LIKE","LOCK","LONG","MAXARCHLOGS",
            "MAXEXTENTS","MINUS","MLSLABEL","MODE","MODIFY","NESTED_TABLE_ID","NESTED_TABLE_SET_REFS","NOAUDIT","NOCOMPRESS","NOCPU_COSTING",
            "NOPARALLEL_INDEX","NOREWRITE","NOT","NOWAIT","NO_ELIMINATE_OUTER_JOIN","NO_FILTERING","NO_PQ_MAP","NULL","NUMBER","OF","OFFLINE","ON",
            "ONLINE","OPTION","OR","ORA_CHECKACL","ORA_GET_ACLIDS","ORA_GET_PRIVILEGES","ORDER","PARALLEL","PARAM","PCTFREE","PRIOR","PRIVILEGE",
            "PRIVILEGES","PUBLIC","RAW","REFERENCING","RENAME","RESOURCE","REVOKE","ROLES","ROW","ROWID","ROWNUM","ROWS","SB4","SELECT","SESSION","SET",
            "SHARE","SIZE","SMALLINT","START","SUCCESSFUL","SYNONYM","SYSDATE","TABLE","THEN","TO","TRIGGER","UB2","UID","UNION","UNIQUE","UPDATE",
            "USER","VALIDATE","VALUES","VARCHAR","VARCHAR2","VIEW","WHENEVER","WHERE","WITH"
       };

        private static bool Exsit(string field)
        {
            if (string.IsNullOrEmpty(field))
            {
                return false;
            }
            else
            {
                field = field.Trim().ToUpper();
                return oracleKeyWord.Contains(field);
            }
        }

        public static void Filter(ref StringBuilder sqlBuilder,string field, ICommandBuilder builder)
        {
            if (OracleKeyWords.Exsit(field))
            {
                sqlBuilder.AppendFormat("{0}{1}{2}"
                    , builder.ObjectNamePrefix
                    , field
                    , builder.ObjectNameSuffix
                    );
            }
            else {
                sqlBuilder.Append(field);
            }
        }

    }
}
