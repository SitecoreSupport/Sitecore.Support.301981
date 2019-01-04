using Sitecore.Data;
using Sitecore.Modules.EmailCampaign.Core;
using Sitecore.Modules.EmailCampaign.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
using ExternalLinksCore = Sitecore.Modules.EmailCampaign.Core;

namespace Sitecore.Support.Modules.EmailCampaign.Core
{
  public class ExternalLinks
  {
    static Type externalLinksType = typeof(ExternalLinksCore.ExternalLinks);
    static MethodInfo ConvertLinkMethod = externalLinksType.GetMethod("ConvertLink", BindingFlags.Static | BindingFlags.NonPublic);
    static MethodInfo GetKeyMethod = externalLinksType.GetMethod("GetKey", BindingFlags.Static | BindingFlags.NonPublic);
    static MethodInfo GetItemValueMethod = externalLinksType.GetMethod("GetItemValue", BindingFlags.Static | BindingFlags.NonPublic);
    static MethodInfo GetDatabasePropertyMethod = externalLinksType.GetMethod("GetDatabaseProperty", BindingFlags.Static | BindingFlags.NonPublic);

    static HashSet<string> linkCache;
    private static Database coreDatabase;

    public static bool IsLinkRegistered(MessageItem message, string link)
    {
      if (linkCache == null)
        linkCache = externalLinksType.GetField("linkCache", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null) as HashSet<string>;

      bool flag;
      bool flag2 = (message != null) && !string.IsNullOrEmpty(link);
      if (flag2)
      {
        HashSet<string> set;
        var originalURL = link;
        link = ConvertLinkMethod.Invoke(null, new object[] { link }) as string;

        //Fix Sitecore.Support.301981: checking against malformed url 
        if (string.IsNullOrWhiteSpace(link))
        {
          return false;
        }

        string key = GetKeyMethod.Invoke(null, new object[] { message, link }) as string;
        lock ((set = linkCache))
        {
          flag2 = !linkCache.Contains(key);
          if (!flag2)
          {
            return true;
          }
        }
        string itemValue = GetItemValueMethod.Invoke(null, new object[] { message }) as string;
        if (!string.IsNullOrEmpty(itemValue))
        {
          string[] separator = new string[] { Environment.NewLine };
          flag2 = !itemValue.Split(separator, StringSplitOptions.None).Contains<string>(link);
          if (!flag2)
          {
            lock ((set = linkCache))
            {
              if (!linkCache.Contains(key))
              {
                linkCache.Add(key);
              }
            }
            return true;
          }
        }
        flag = GetDatabasePropertyMethod.Invoke(null, new object[] { CoreDatabase, key }) as string == link;
      }
      else
      {
        flag = false;
      }
      return flag;
    }
    private static Database CoreDatabase
    {
      get
      {
        return (coreDatabase ?? (coreDatabase = Database.GetDatabase("core")));
      }
    }

  }
}