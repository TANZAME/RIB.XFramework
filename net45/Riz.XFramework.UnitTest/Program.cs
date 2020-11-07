﻿
using System;
using System.Linq;
using Riz.XFramework.Data;
using System.Data;

namespace Riz.XFramework.UnitTest
{
    public class Program
    {
        [MTAThread]
        //[STAThread]
        public static void Main(string[] args)
        {
            // fase 表示忽略 ColumnAttribute.Name，否则翻译时会使用 ColumnAttribute.Name
            bool withNameAttribute = false;  
            // 是否调式模式，调试模式下生成的 SQL 会有格式化
            bool isDebug = true;
            // 是否区分大小写，适用 ORCACLE 和 POSTGRESQL
            bool caseSensitive = false;
            ITest test = null;
            string fileName = string.Empty;
            DatabaseType databaseType = DatabaseType.None;
            string s = args != null && args.Length > 0 ? args[0] : null;
            if (!string.IsNullOrEmpty(s)) databaseType = (DatabaseType)Convert.ToByte(s);

            // 命令拦截
            var interceptor = new DbCommandInterceptor
            {
                OnExecuting = cmd =>
                {
                    var writer = System.IO.File.AppendText(fileName);
                    writer.WriteLine(cmd.CommandText);
                    if (cmd.Parameters != null)
                    {
                        for (int i = 0; i < cmd.Parameters.Count; i++)
                        {
                            IDbDataParameter p = (IDbDataParameter)cmd.Parameters[i];
                            writer.Write("-- ");
                            writer.Write(p.ParameterName);
                            writer.Write(" = ");
                            writer.Write(p.Value == null ? string.Empty : (p.Value is byte[] ? Common.BytesToHex((byte[])p.Value, true, true) : p.Value));
                            writer.Write(", DbType = {0}, ", p.DbType);
                            if (p.Size != default(int)) writer.Write("Size = {0}, ", p.Size);
                            if (p.Precision != default(byte)) writer.Write("Precision = {0}, ", p.Precision);
                            if (p.Scale != default(byte)) writer.Write("Scale = {0}, ", p.Scale);
                            if (p.Direction != ParameterDirection.Input) writer.Write("Direction = {0}, ", p.Direction);
                            writer.WriteLine();
                            if (i == cmd.Parameters.Count - 1) writer.WriteLine();
                        }
                    }

                    writer.Close();
                },
                OnExecuted = cmd => { }
            };
            DbInterception.Add(interceptor);

            foreach (DatabaseType item in Enum.GetValues(typeof(DatabaseType)))
            {
                if (item == DatabaseType.None) continue;

                DatabaseType myDatabaseType = item;
                if (!string.IsNullOrEmpty(s)) myDatabaseType = databaseType;

                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                fileName = baseDirectory + @"\Log_" + myDatabaseType + ".sql";
                if (System.IO.File.Exists(fileName)) System.IO.File.Delete(fileName);

                var obj = Activator.CreateInstance(null, string.Format("Riz.XFramework.UnitTest.{0}.{1}{0}Test", myDatabaseType, withNameAttribute ? "Riz" : string.Empty));
                test = (ITest)(obj.Unwrap());
                test.IsDebug = isDebug;
                test.CaseSensitive = caseSensitive;


                if (test != null)
                {
                    Console.WriteLine(myDatabaseType + " BEGIN");
                    test.Run(myDatabaseType);
                    Console.WriteLine(myDatabaseType + " END");
                }

                if (!string.IsNullOrEmpty(s)) break;
            }

            Console.WriteLine("回车退出~");
            Console.ReadLine();
        }
    }
}