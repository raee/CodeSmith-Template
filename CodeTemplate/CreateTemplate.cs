using System;
using System.ComponentModel;
using System.Drawing.Design;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms.Design;
using System.Xml;
using CodeSmith.Engine;
using SchemaExplorer;
using System.Diagnostics;
using System.Data;

namespace CR.CodeTemplate
{
    public class CreateTemplate : CodeSmith.Engine.CodeTemplate
    {
        //private string _tablename;
        private string _cutFirstTableName = "tb_";
        private string _cutColumnName = "db_";
        private string _xmlTemplatePath = "template.xml";
        private string _mapping = "System-CSharpAlias.csmap";

        [DisplayName("Xml配置文件路径"), Description("模板将赋值到XML模板配置，对特殊的代码赋值"), Category("02 - 配置")]
        public string XmlTemplatePath
        {
            get
            {
                return _xmlTemplatePath;
            }
            set
            {
                _xmlTemplatePath = value;
            }
        }
        
        [DisplayName("Mapping文件路径"), Category("02 - 配置")]
        public string MappingPath
        {
            get
            {
                return _mapping;
            }
            set
            {
                _mapping = value;
            }
        }

        /// <summary>
        ///     实体命名空间
        /// </summary>
        [DisplayName("模型命名空间"), Description("实体的命名空间"), Category("01 - 部署")]
        public virtual string ModelNamespace
        {
            get;
            set;
        }

        /// <summary>
        ///     表名
        /// </summary>
        public virtual string TableName
        {
            get
            {
//                if (string.IsNullOrEmpty(_tablename))
//                {
                    return DbUtil.GetTableName(CurrentTable.Name, CutFirstTableName);
                    // _tablename = CurrentTable.Name;
//                }
//                return _tablename;
            }
        }

        [DisplayName("数据库"), Description("请选择数据库"), Category("01 - 部署")]
        public DatabaseSchema DataBase
        {
            get;
            set;
        }


        [DisplayName("文件输出路径")]
        [Category("01 - 部署")]
        [Editor(typeof(FolderNameEditor), typeof(UITypeEditor))]
        public virtual string OutputDirectory
        {
            get;
            set;
        }

        [DisplayName("数据库表")]
        [Description("不需要选择")]
        [Optional, NotChecked]
        // [Browsable(false)] // 不显示在属性栏中
        public virtual TableSchema CurrentTable
        {
            get;
            set;
        }


        [DisplayName("去掉的表首字母")]
        [Category("02 - 配置")]
        [NotChecked, Optional]
        [DefaultValue("_")]
        public string CutFirstTableName
        {
            get
            {
                return _cutFirstTableName;
            }
            set
            {
                _cutFirstTableName = value;
            }
        }


        [DisplayName("去掉的列首字母")]
        [Category("02 - 配置")]
        [NotChecked, Optional]
        [DefaultValue("_")]
        public string CutColumnName
        {
            get
            {
                return _cutColumnName;
            }
            set
            {
                _cutColumnName = value;
            }
        }

        public CreateTemplate()
        {
            DbUtil.TEMPLATE = this;
        }

        /// <summary>
        ///     遍历列
        /// </summary>
        /// <param name="cols">列集合</param>
        /// <param name="format">模板</param>
        /// <returns></returns>
        public string ForEach(ColumnSchemaCollection cols, string format)
        {
            var sb = new StringBuilder();
            foreach (var col in cols)
            {

                var isPrimaryKeyMember = col.IsPrimaryKeyMember; // 是否为主键列
                var m = new DataModel
                {
                    TableComment = DbUtil.GetComment(CurrentTable.Description, TableName),
                    ColumnName = DbUtil.GetColumnName(col.Name, CutColumnName),
                    ColumnDbType = col.NativeType,
                    ColumnDbName = col.Name,
                    ColumnIsPrimarykey = col.IsPrimaryKeyMember,
                    ColumnTypeNotNull = DbUtil.GetColumnType(col.SystemType),
                    ColumnType = DbUtil.GetColumnType(col.SystemType),
                    ColumnLength = (col.Size < 0 ? int.MaxValue : col.Size).ToString(CultureInfo.InvariantCulture),
                    ColumnEnableNull = col.AllowDBNull,
                    ColumnIdentity = Convert.ToBoolean(col.ExtendedProperties["CS_IsIdentity"].Value.ToString()),
                    ColumnDefaultValue = col.ExtendedProperties["CS_Default"].Value.ToString(),
                    TableName = TableName
                };
                m.ColumnComment = col.Description;
                m.ColumnComment = DbUtil.GetComment(col.Description, m.ColumnName);
                if (isPrimaryKeyMember)
                    m.ColumnComment += "主键列";

                m.ColumnType = m.ColumnEnableNull ? m.ColumnType + "?" : m.ColumnType;

                sb.AppendLine(DbUtil.Format(format, m));
            }
            return sb.ToString();
        }

        /// <summary>
        ///     子表遍历
        /// </summary>
        /// <param name="cols"></param>
        /// <param name="format"></param>
        /// <returns></returns>
        public string ForEach(TableKeySchemaCollection cols, string format)
        {
            var sb = new StringBuilder();
            foreach (var col in cols)
            {
                var parentTable = col.PrimaryKeyTable;
                var childrenTable = col.ForeignKeyTable;

                var tagTable = parentTable.Equals(CurrentTable) ? childrenTable : parentTable;
                var m = new DataModel
                {
                    ChildrenTableName = DbUtil.GetTableName(tagTable.Name, CutFirstTableName),
                };

                m.ChildrenTableComment = DbUtil.GetComment(tagTable.Description, m.ChildrenTableName);

                m.PerentTableName = m.ChildrenTableName;
                m.ParentTableComment = m.ChildrenTableComment;


                sb.AppendLine(DbUtil.Format(format, m));
            }
            return sb.ToString();
        }

        public string ForEachColumn(string name, bool enablePrimary, bool enableIndentity)
        {
            string format = GetTemplate(name);
            var result = new ColumnSchemaCollection();
            ColumnSchemaCollection cols = enablePrimary ? CurrentTable.Columns : CurrentTable.NonPrimaryKeyColumns;


            if (!enableIndentity)
            {
                // 去掉自动增长列
                foreach (ColumnSchema item in cols)
                {
                    if (item.ExtendedProperties["CS_IsIdentity"].Value.ToString().Equals("True"))
                        continue;
                    result.Add(item);
                }
            }
            else
            {
                result = cols;
            }
            return ForEach(result, format);
        }

        /// <summary>
        ///     遍历所有的列
        /// </summary>
        /// <param name="name"></param>
        /// <param name="enablePrimary"></param>
        /// <returns></returns>
        public string ForEachColumn(string name, bool enablePrimary)
        {
            string format = GetTemplate(name);
            ColumnSchemaCollection cols = enablePrimary ? CurrentTable.Columns : CurrentTable.NonPrimaryKeyColumns;
            return ForEach(cols, format);
        }

        public string ForEachPrimaryColumn(string name)
        {
            string format = GetTemplate(name);
            return ForEach(CurrentTable.PrimaryKey.MemberColumns, format);
        }

        /// <summary>
        ///     父表遍历
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public string ForEachPerentColumn(string name)
        {
            string format = GetTemplate(name);
            return ForEach(CurrentTable.ForeignKeys, format);
        }


        public string ForEachChildrenColumn(string name)
        {
            string format = GetTemplate(name);
            return ForEach(CurrentTable.PrimaryKeys, format);
        }

        /// <summary>
        ///  获取模板
        /// </summary>
        /// <param name="itemName">模板名称</param>
        /// <returns></returns>
        public string GetTemplate(string itemName)
        {
            var doc = new XmlDocument();
            doc.Load(XmlTemplatePath);
            if (doc.DocumentElement != null)
            {
                var root = doc.DocumentElement.ParentNode;
                if (root != null)
                {
                    var nodes = root.SelectNodes("//item[@name='" + itemName + "']");
                    if (nodes == null || nodes.Count <= 0)
                    {
                        return "null";
                    }

                    return nodes[0].InnerText;
                }
            }
            else
            {
                throw new Exception("获取模板失败,请检查XML文件路径是否正确！");
            }
            return "";
        }

    }

    /// <summary>
    /// 数据库帮助工具
    /// </summary>
    public static class DbUtil
    {
        public static CreateTemplate TEMPLATE;
        
        public static string GetComment(string comment, string name)
        {
            return string.IsNullOrEmpty(comment) ? name : comment;
        }

        /// <summary>
        /// 获取注释说明
        /// </summary>
        /// <returns></returns>
        public static string GetComment(ISchemaObject col, string cut)
        {
            return string.IsNullOrEmpty(col.Description) ? DbUtil.GetColumnName(col.Name, cut) : col.Description;
        }

        /// <summary>
        ///     获取表名
        /// </summary>
        /// <param name="name"></param>
        /// <param name="cut"></param>
        /// <returns></returns>
        public static string GetTableName(string name, string cut)
        {
            name = name.RemoveSplit("_");
            name = name.RemoveFistChat(cut);
            name = name.ToFirstUpper();
            return name;
        }

        /// <summary>
        ///     获取列名
        /// </summary>
        /// <param name="name"></param>
        /// <param name="cut"></param>
        /// <returns></returns>
        public static string GetColumnName(string name, string cut)
        {
            name = name.RemoveSplit("_");
            name = name.RemoveFistChat(cut);
            name = name.ToFirstUpper();
            return name;
        }

        /// <summary>
        /// 是否为主键列
        /// </summary>
        /// <param name="col"></param>
        /// <returns></returns>
        public static bool isPrimaryKey(ColumnSchema col)
        {
            return col.IsPrimaryKeyMember;
        }
        
        public static bool isDateType(ColumnSchema col)
        {
            return col.SystemType.Equals(typeof(System.DateTime));
        }
        /// <summary>
        /// 是否为自增列
        /// </summary>
        /// <param name="col"></param>
        /// <returns></returns>
        public static bool isAutoIdentity(ISchemaObject col)
        {
            return Convert.ToBoolean(col.ExtendedProperties["CS_IsIdentity"].Value.ToString());
        }
        
        /// <summary>
        /// Sqlite 是否为自增列
        /// </summary>
        /// <param name="col"></param>
        /// <returns></returns>
        public static bool isAutoIdentitySqlite(ColumnSchema col)
        {
            // 主键是Integer类型的为自增
            bool isInt = col.ExtendedProperties["CS_SQLiteType"].Value.ToString().Equals("integer");
            return isInt && DbUtil.isPrimaryKey(col);
        }

        /// <summary>
        /// 获取列的默认值
        /// </summary>
        /// <param name="col"></param>
        /// <returns></returns>
        public static string getDefaultValue(ISchemaObject col)
        {
           
           var ext = col.ExtendedProperties["CS_Default"];
            if(ext == null)
                return "";
            else
                return ext.Value.ToString();
           
        }

        /// <summary>
        /// 是否有父表
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        public static bool hasParentTable(TableSchema table)
        {
            return table.ForeignKeys.Count > 0;
        }

        /// <summary>
        /// 是否有子表
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        public static bool hasChilrentTable(TableSchema table)
        {
            return table.PrimaryKeys.Count > 0;
        }
        
        public static string getJavaType(Type type, string map)
        {
             try
            {
                var result = Map.LoadFromName(map)[type.FullName];
                
                return result;
            }
            catch (Exception)
            {
                throw new Exception("Map System-CSharpAlias.csmap没有找到！");
            }
        }
        
        /// <summary>
        /// 是否有日期类型
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        public static bool hasDateType(TableSchema table)
        {
            bool result = false;
            foreach(var col in table.Columns)
            {
                if (col.DataType == DbType.DateTime || col.DataType == DbType.Date || col.DataType == DbType.DateTime2 || col.DataType == DbType.DateTimeOffset)
                {
                    result = true;
                    break;
                }

            }
            return result;
        }

        /// <summary>
        /// 获取列的长度
        /// </summary>
        /// <param name="col"></param>
        /// <returns></returns>
        public static string getColumnLength(IDataObject col)
        {
            return (col.Size < 0 ? int.MaxValue : col.Size).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 获取系统类型
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string GetColumnType(CreateTemplate template, Type type)
        {
            
            try
            {
                var result = Map.LoadFromName(template.MappingPath)[type.FullName];
                
                return result;
            }
            catch (Exception)
            {
                throw new Exception("Map System-CSharpAlias.csmap没有找到！");
            }
        }
        
        /// <summary>
        /// 获取系统类型
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string GetColumnType(Type type)
        {
            return GetColumnType(TEMPLATE,type);
        }
        

        /// <summary>
        /// 格式化输出，根据模板特定的字段输出
        /// </summary>
        /// <param name="format"></param>
        /// <param name="m"></param>
        /// <returns></returns>
        public static string Format(string format, DataModel m)
        {

            format = format.Replace("table.name", m.TableName);
            format = format.Replace("table.comment", m.TableComment);

            format = format.Replace("fk.name", m.PerentTableName);
            format = format.Replace("fk.comment", m.ParentTableComment);

            format = format.Replace("col.name", m.ColumnName);
            format = format.Replace("col.dbname", m.ColumnDbName);
            format = format.Replace("col.type", m.ColumnTypeNotNull);
            format = format.Replace("col.nulltype", m.ColumnType);
            format = format.Replace("col.comment", m.ColumnComment);
            format = format.Replace("col.dbtype", m.ColumnDbType);
            format = format.Replace("col.length", m.ColumnLength);
            format = format.Replace("col.default", m.ColumnDefaultValue);
            format = format.Replace("col.isidentity", m.ColumnIdentity.ToString().ToLower());
            format = format.Replace("col.enableNull", m.ColumnEnableNull.ToString().ToLower());
            format = format.Replace("col.isPrimarykey", m.ColumnIsPrimarykey.ToString().ToLower());

            return format;
        }

        public static string CutNextLine(string start, string srouce)
        {
            string src = srouce;
            int startIndex = src.IndexOf(start, System.StringComparison.Ordinal); //开始索引
            src = src.Substring(startIndex); //截断
            int fristIndex = src.IndexOf("\r\n", startIndex, System.StringComparison.Ordinal);//下一行的索引：两个\r\n
            src = src.Substring(fristIndex); //截断
            int endIndex = src.IndexOf("\r\n", startIndex, System.StringComparison.Ordinal);//下一行的索引：两个\r\n
            return srouce.Remove(startIndex).Remove(endIndex);
        }
    }



    /// <summary>
    /// 数据库实体映射字段模型
    /// </summary>
    public class DataModel
    {
        /// <summary>
        /// 父表表名
        /// </summary>
        public string PerentTableName
        {
            get;
            set;
        }

        /// <summary>
        /// 父表说明
        /// </summary>
        public string ParentTableComment
        {
            get;
            set;
        }

        /// <summary>
        /// 子表表名
        /// </summary>
        public string ChildrenTableName
        {
            get;
            set;
        }

        /// <summary>
        /// 子表说明
        /// </summary>
        public string ChildrenTableComment
        {
            get;
            set;
        }

        /// <summary>
        /// 没有带问号的字段类型
        /// </summary>
        public string ColumnTypeNotNull
        {
            get;
            set;
        }

        /// <summary>
        /// 表名
        /// </summary>
        public string TableName
        {
            get;
            set;
        }

        /// <summary>
        /// 表名说明
        /// </summary>
        public string TableComment
        {
            get;
            set;
        }

        /// <summary>
        /// 列是否是自动增长
        /// </summary>
        public bool ColumnIdentity
        {
            get;
            set;
        }

        /// <summary>
        /// 列名
        /// </summary>
        public string ColumnName
        {
            get;
            set;
        }

        /// <summary>
        /// 数据库列名
        /// </summary>
        public string ColumnDbName
        {
            get;
            set;
        }

        /// <summary>
        /// 列名说明
        /// </summary>
        public string ColumnComment
        {
            get;
            set;
        }

        /// <summary>
        /// 列队数据类型
        /// </summary>
        public string ColumnType
        {
            get;
            set;
        }

        /// <summary>
        /// 列的数据库类型
        /// </summary>
        public string ColumnDbType
        {
            get;
            set;
        }

        /// <summary>
        /// 列长度
        /// </summary>
        public string ColumnLength
        {
            get;
            set;
        }

        /// <summary>
        ///     字段是否允许为空
        /// </summary>
        public bool ColumnEnableNull
        {
            get;
            set;
        }

        /// <summary>
        /// 字段默认值
        /// </summary>
        public string ColumnDefaultValue
        {
            get;
            set;
        }

        /// <summary>
        /// 是否为主键列
        /// </summary>
        public bool ColumnIsPrimarykey
        {
            get;
            set;
        }
    }

    public static class GenarateUtil
    {
        /// <summary>
        /// 为模板属性赋值
        /// </summary>
        /// <param name="template"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public static void SetProperty(CreateTemplate current, CreateTemplate template)
        {
            template.SetProperty("ModelNamespace", current.ModelNamespace);
            template.SetProperty("OutputDirectory", current.OutputDirectory);
            template.SetProperty("DataBase", current.DataBase);
            template.SetProperty("CutColumnName", current.CutColumnName);
            template.SetProperty("CutFirstTableName", current.CutFirstTableName);
            template.SetProperty("MappingPath", current.MappingPath);
        }

        /// <summary>
        /// 为模板表属性赋值
        /// </summary>
        /// <param name="template"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public static void SetPropertyTable(CreateTemplate template, TableSchema table)
        {
            template.SetProperty("CurrentTable", table);
        }

        /// <summary>
        /// 输出Java格式的文件夹
        /// </summary>
        /// <param name="current"></param>
        /// <param name="fileName"></param>
        public static void ResponseByJavaDir(CreateTemplate current,CreateTemplate template, string fileName)
        {
            string path = template.OutputDirectory;
            path += System.IO.Path.DirectorySeparatorChar;
            path += fileName;
            path += ".java";
           
            template.RenderToFile(path, true);
            Debug.WriteLine("正在输出："+path);
           // current.Response.WriteLine(path);
        }
    }

    /// <summary>
    /// 字符串帮助类
    /// </summary>
    public static class StringUtil
    {
        /// <summary>
        ///     首字母大写
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string ToFirstUpper(this string name)
        {
            if (name.Length > 1)
            {
                var up = name.Substring(0, 1).ToUpper();
                return up + name.Substring(1);
            }

            return name;
        }


        /// <summary>
        ///     移除特定的分隔符
        /// </summary>
        /// <param name="name"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        public static string RemoveSplit(this string name, string tag)
        {
            if (name == null)
                throw new ArgumentNullException("name");
            if (name.Contains(tag))
            {
                string[] splits = name.Split(tag.ToArray());
                var sb = new StringBuilder();
                foreach (string item in splits)
                {
                    sb.Append(item.ToFirstUpper());
                }
                return sb.ToString();
            }
            return name;
        }

        /// <summary>
        ///     移除前缀
        /// </summary>
        /// <param name="name"></param>
        /// <param name="tag">前缀</param>
        /// <returns></returns>
        public static string RemoveFistChat(this string name, string tag)
        {
            if (name.Contains(tag))
            {
                name = name.Replace(tag, string.Empty);
            }

            return name;
        }
    }
}