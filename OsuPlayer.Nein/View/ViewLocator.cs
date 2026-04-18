using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Nein.Base;
using Nein.Extensions;
using Splat;

namespace Nein.View;

public class ViewLocator : IDataTemplate
{
    public bool SupportsRecycling => false;

    public Control Build(object data)
    {
        var name = data.GetType().FullName?.Replace("ViewModel", "View");

        if (name == null)
            return OnFail("");

        var type = Assembly.GetAssembly(data.GetType())?.GetType(name);

        if (Locator.Current.GetService(type) is Control serviceView) return serviceView;
        if (type != null)
        {
            try
            {
                if (Activator.CreateInstance(type) is Control view) return view;
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException ?? ex;
                System.Diagnostics.Debug.WriteLine($"[ViewLocator] Failed to create {name}: {inner}");
                Console.Error.WriteLine($"[ViewLocator] Failed to create {name}: {inner}");
                throw;
            }
        }
        return OnFail(name);
    }

    public bool Match(object data)
    {
        return data is BaseViewModel;
    }

    private Control OnFail(string name)
    {
        var button = new Button
        {
            Content = $"Not Found: {name}",
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        return button;
    }
}
