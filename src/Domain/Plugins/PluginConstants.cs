namespace Domain.Plugins;

public static class PluginConstants
{
    public const int MaxPluginsPerEvent = 5;
    public const int CodeMaxLength = 50;
    public const int DataKeyMaxLength = 100;
    public const int AgendaTitleMaxLength = 200;
    public const int AgendaDescriptionMaxLength = 2000;
    public const int AgendaSpeakerMaxLength = 200;
    public const int FaqQuestionMaxLength = 500;
    public const int FaqAnswerMaxLength = 4000;
    public const int LinkLabelMaxLength = 200;
    public const int LinkDescriptionMaxLength = 1000;
    public const int LinkUrlMaxLength = 500;

    public const string CodeAgenda = "agenda";
    public const string CodeFaq = "faq";
    public const string CodeLinks = "links";

    public const string DataKeySessions = "sessions";
    public const string DataKeyEntries = "entries";
    public const string DataKeyLinks = "links";

    public static readonly Guid AgendaPluginId = Guid.Parse("a0000001-0001-4000-8000-000000000001");
    public static readonly Guid FaqPluginId = Guid.Parse("a0000002-0001-4000-8000-000000000002");
    public static readonly Guid LinksPluginId = Guid.Parse("a0000003-0001-4000-8000-000000000003");
}
