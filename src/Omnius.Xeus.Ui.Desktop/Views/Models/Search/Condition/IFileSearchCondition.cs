namespace Omnius.Xeus.Ui.Desktop.Views.Models.Search.Condition
{
    public enum FileSearchConditionType
    {
        Allow,
        Deny,
    }

    public interface IFileSearchCondition<T>
    {
        FileSearchConditionType Type { get; }

        bool IsMatch(T value);
    }
}
