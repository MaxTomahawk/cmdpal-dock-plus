using CmdPalDockPlus.Core.Profiles;
using CmdPalDockPlus.Core.Rules;
using CmdPalDockPlus.Core.Templates;

namespace CmdPalDockPlus.Providers;

public static class ProfileFieldDependencies
{
    public static IReadOnlySet<string> Resolve(AppProfile profile)
    {
        HashSet<string> fields = new(StringComparer.Ordinal);
        AddTemplate(profile.Display.Title);
        AddTemplate(profile.Display.Subtitle);
        if (!string.IsNullOrWhiteSpace(profile.Display.Icon)) AddTemplate(profile.Display.Icon!);

        foreach (var rule in profile.Rules)
        {
            foreach (var condition in rule.Conditions) fields.Add(condition.FieldId);
            foreach (var action in rule.Actions)
            {
                switch (action)
                {
                    case GroupAction group: AddTemplate(group.Key); break;
                    case SetTitleTemplateAction title: AddTemplate(title.Template); break;
                    case SetSubtitleTemplateAction subtitle: AddTemplate(subtitle.Template); break;
                    case SetIconTemplateAction icon: AddTemplate(icon.Template); break;
                }
            }
        }
        return fields;

        void AddTemplate(string template)
        {
            foreach (var field in TemplateCompiler.Compile(template).Dependencies) fields.Add(field);
        }
    }
}
