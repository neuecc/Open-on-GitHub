using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace OpenOnGitHub.SourceLinkInternals
{
  public static class WpfHelper
  {
    public static T FindChild<T>(DependencyObject parent, string childName) where T : FrameworkElement
    {
      var obj = default (T);
      try
      {
        obj = parent != null ? FindVisualChildren<T>(parent).FirstOrDefault(x => x.Name == childName) : default (T);
      }
      catch (Exception ex)
      {
      }
      return obj;
    }

    public static IEnumerable<T> FindVisualChildren<T>(DependencyObject dependencyObject) where T : DependencyObject
    {
        if (dependencyObject == null) yield break;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(dependencyObject); i++)
        {
            var child = VisualTreeHelper.GetChild(dependencyObject, i);
            if (child is T o)
            {
                yield return o;
            }

            foreach (var childOfChild in FindVisualChildren<T>(child))
            {
                yield return childOfChild;
            }
        }
    }

    public static IEnumerable<DependencyObject?> GetObjectsByTypeName(DependencyObject? obj, string name)
    {
      if (obj == null)
        yield break;

      if (obj.ToString().StartsWith(name))
        yield return obj;

      var childrenCount = VisualTreeHelper.GetChildrenCount(obj);

      for (var childIndex = 0; childIndex < childrenCount; ++childIndex)
      {
          foreach (var @object in GetObjectsByTypeName(VisualTreeHelper.GetChild(obj, childIndex), name))
          {
              yield return @object;
          }
      }
    }
  }
}