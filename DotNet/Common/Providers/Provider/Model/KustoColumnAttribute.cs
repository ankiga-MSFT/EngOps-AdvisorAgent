namespace Provider.Model
{
    [AttributeUsage(AttributeTargets.Property)]
    public class KustoColumnAttribute : Attribute
    {
        public string ColumnName { get; }

        public KustoColumnAttribute(string columnName)
        {
            ColumnName = columnName;
        }
    }

}
