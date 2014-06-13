using System;
using System.ComponentModel;
using System.Text;
using System.Xml;
using SchemaExplorer;

namespace CR.CodeSimth.Nhibernate
{
    public delegate void EacheColumnHandler(ColumnSchema col, DataModel m, StringBuilder sb);

    public class BaseTemplate : CRCodeSimth
    {
        #region 字段

        private string _assembleName;

        #endregion

        #region 属性

        protected string XmlTemplatePath = "./NhibernateMappingModel.xml";
        private string _modelName;

        [DisplayName("实体层命名空间")]
        [Category("02 - 配置")]
        public override string ModelNamespace
        {
            get { return GetDefault(ref _modelName, AssemblyName + ".Models"); }
            set { _modelName = value; }
        }

        [DisplayName("程序集名字")]
        [Category("02 - 配置")]
        [Editor]
        public string AssemblyName
        {
            get { return GetDefault(ref _assembleName, "MyAssembly"); }
            set { _assembleName = value; }
        }


        [DisplayName("主键显示为Id")]
        [DefaultValue(true)]
        public bool ForId { get; set; }

        #endregion

        //public event EacheColumnHandler OnEachColumn;


        /// <summary>
        ///     获取模板
        /// </summary>
        /// <param name="itemName">模板名称</param>
        /// <returns></returns>
        public string GetTemplate(string itemName)
        {
            var doc = new XmlDocument();
            doc.Load(XmlTemplatePath);
            XmlNode root = doc.DocumentElement.ParentNode;
            XmlNodeList nodes = root.SelectNodes("//item[@name='" + itemName + "']");
            if (nodes == null || nodes.Count <= 0)
            {
                return "null";
            }

            return nodes[0].InnerText;
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
            foreach (ColumnSchema col in cols)
            {
                bool isPrimaryKeyMember = col.IsPrimaryKeyMember; // 是否为主键列
                var m = new DataModel
                {
                    TableName = base.TableName,
                    TableComment = DbUtil.GetComment(base.Table.Description, base.TableName),
                    ColumnName = DbUtil.GetColumnName(col.Name, this.CutColumnName),
                    ColumnDbType = col.NativeType,
                    ColumnType = DbUtil.GetColumnType(col.SystemType),
                    ColumnLength = (col.Size < 0 ? int.MaxValue : col.Size).ToString(),
                    ColumnEnableNull = col.AllowDBNull,
                    ColumnIdentity = Convert.ToBoolean(col.ExtendedProperties["CS_IsIdentity"].Value.ToString())
                };

                m.ColumnComment = DbUtil.GetComment(col.Description, m.ColumnName);
                m.ColumnComment = isPrimaryKeyMember ? m.ColumnComment + " 主键" : m.ColumnComment;

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
            foreach (TableKeySchema col in cols)
            {
                TableSchema tagTable;
                TableSchema parentTable = col.PrimaryKeyTable;
                TableSchema childrenTable = col.ForeignKeyTable;

                tagTable = parentTable.Equals(base.Table) ? childrenTable : parentTable;
                var m = new DataModel
                {
                    ChildrenTableName = DbUtil.GetTableName(tagTable.Name, this.CutFirstTableName),
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
            ColumnSchemaCollection cols = enablePrimary ? base.Table.Columns : base.Table.NonPrimaryKeyColumns;


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
            ColumnSchemaCollection cols = enablePrimary ? base.Table.Columns : base.Table.NonPrimaryKeyColumns;
            return ForEach(cols, format);
        }

        public string ForEachPrimaryColumn(string name)
        {
            string format = GetTemplate(name);
            return ForEach(base.Table.PrimaryKey.MemberColumns, format);
        }

        /// <summary>
        ///     父表遍历
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public string ForEachPerentColumn(string name)
        {
            string format = GetTemplate(name);
            return ForEach(base.Table.ForeignKeys, format);
        }


        public string ForEachChildrenColumn(string name)
        {
            string format = GetTemplate(name);
            return ForEach(base.Table.PrimaryKeys, format);
        }
    }
}