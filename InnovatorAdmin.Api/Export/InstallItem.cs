﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace InnovatorAdmin
{
  public class InstallItem : IDiffFile
  {
    public const string ScriptType = "*Script";

    private IEnumerable<ItemReference> _dependencies;
    private XmlElement _elem;
    private ItemReference _itemRef;
    private string _name;
    private InstallType _type;

    internal IEnumerable<ItemReference> CoreDependencies { get { return _dependencies; } }
    public InstallType Type {
      get { return _type; }
      set { _type = value; }
    }
    public ItemReference Reference { get { return _itemRef; } }
    public string InstalledId { get; set; }
    public string Name
    {
      get
      {
        if (_name == null)
        {
          switch (_type)
          {
            case InstallType.Create:
              return "Install of " + this.Reference.ToString();
            case InstallType.DependencyCheck:
              return "Check of Dependency " + this.Reference.ToString();
            case InstallType.Script:
              return this.Reference.KeyedName;
          }
        }
        return _name;
      }
      set { _name = value; }
    }
    public string Path { get; set; }
    public XmlElement Script { get { return _elem; } }

    string IDiffFile.Path
    {
      get
      {
        if (!string.IsNullOrWhiteSpace(this.Path))
          return this.Path;
        var folder = Type == InstallType.Script ? "_Scripts" : Reference.Type;
        return folder + "\\" + Utils.CleanFileName((Reference.KeyedName ?? "") + "_" + Reference.Unique) + ".xml";
      }
    }

    private string _compare;
    IComparable IDiffFile.CompareKey
    {
      get
      {
        if (_compare == null)
        {
          using (var md5 = new MD5CryptoServiceProvider())
          using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(Script.OuterXml)))
          {
            var result = new StringBuilder(32);
            var hash = md5.ComputeHash(stream);
            for (var i = 0; i < hash.Length; i++)
            {
              result.AppendFormat("{0:x2}", hash[i]);
            }
            return result.ToString();
          }
        }
        return _compare;
      }
    }

    public InstallItem() { }

    public void SetScript(string script)
    {
      var aml = _elem.OwnerDocument.CreateElement("AML");
      aml.InnerXml = script;
      if (aml.Elements().Count() == 1)
      {
        _elem = aml.Elements().Single();
      }
      else
      {
        _elem = aml;
      }
    }

    public override string ToString()
    {
      return this.Name;
    }

    public static InstallItem FromScript(XmlElement elem, string path)
    {
      var result = FromScript(elem);
      result.Path = path;
      return result;
    }
    public static InstallItem FromScript(XmlElement elem
      , Func<XmlElement, string> keyedNameGetter = null)
    {

      var result = new InstallItem();
      result._elem = elem;
      result._itemRef = ItemReference.FromFullItem(elem, true);
      if (result._itemRef.Type.IsGuid())
      {
        result.InstalledId = result._itemRef.Type;
      }
      else
      {
        result.InstalledId = elem.Attribute("id", "");
      }

      if (elem.HasAttribute("_dependency_check"))
      {
        result._type = InstallType.DependencyCheck;
      }
      else if (elem.HasAttribute("action"))
      {
        switch (elem.Attributes["action"].Value)
        {
          case "add":
          case "merge":
          case "create":
            result._type = InstallType.Create;
            break;
          case "ActivateActivity":
          case "AddItem":
          case "AddHistory":
          case "ApplyUpdate":
          case "BuildProcessReport":
          case "CancelWorkflow":
          case "checkImportedItemType":
          case "closeWorkflow":
          case "copy":
          case "copyAsIs":
          case "copyAsNew":
          case "delete":
          case "edit":
          case "EmailItem":
          case "EvaluateActivity":
          case "exportItemType":
          case "get":
          case "getItemAllVersions":
          case "getAffectedItems":
          case "getItemConfig":
          case "getItemLastVersion":
          case "getItemNextStates":
          case "getItemRelationships":
          case "GetItemRepeatConfig":
          case "getItemWhereUsed":
          case "GetMappedPath":
          case "getPermissions":
          case "getRelatedItem":
          case "GetUpdateInfo":
          case "instantiateWorkflow":
          case "lock":
          case "New Workflow Map":
          case "PromoteItem":
          case "purge":
          case "recache":
          case "replicate":
          case "resetAllItemsAccess":
          case "resetItemAccess":
          case "resetLifecycle":
          case "setDefaultLifecycle":
          case "skip":
          case "startWorkflow":
          case "unlock":
          case "update":
          case "ValidateWorkflowMap":
          case "version":
            if ((elem.Attributes["type"].Value != "Form" && elem.Attributes["type"].Value != "View")
              || elem.Attributes["action"].Value != "delete")
              result._dependencies = Enumerable.Repeat(result._itemRef, 1);
            result._itemRef = new ItemReference(ScriptType, result._itemRef.ToString() + " " + Utils.GetChecksum(Encoding.UTF8.GetBytes(elem.OuterXml)))
            {
              KeyedName = RenderAttributes(elem)
            };
            result._type = InstallType.Script;
            break;
          default:
            result._dependencies = Enumerable.Repeat(new ItemReference("Method", "[Method].[name] = '" + elem.Attributes["action"].Value + "'")
            {
              KeyedName = elem.Attributes["action"].Value
            }, 1);
            result._itemRef = new ItemReference(ScriptType, result._itemRef.ToString() + " " + Utils.GetChecksum(Encoding.UTF8.GetBytes(elem.OuterXml)))
            {
              KeyedName = RenderAttributes(elem)
            };
            result._type = InstallType.Script;
            break;
        }
      }

      if (elem.Attribute(XmlFlags.Attr_IsScript) == "1")
      {
        if (string.IsNullOrEmpty(result._itemRef.KeyedName))
        {
          result._itemRef.KeyedName = RenderAttributes(elem);
        }
        result._type = InstallType.Script;
      }
      return result;
    }
    public static InstallItem FromDependency(ItemReference itemRef)
    {
      var result = new InstallItem();
      result._itemRef = itemRef;
      result._elem = new XmlDocument().CreateElement("Item");
      result._elem.SetAttribute("type", result._itemRef.Type);
      if (result._itemRef.Unique.IsGuid())
      {
        result._elem.SetAttribute("id", result._itemRef.Unique);
      }
      else
      {
        result._elem.SetAttribute("where", result._itemRef.Unique);
      }
      result._elem.SetAttribute("action", "get");
      result._elem.SetAttribute("_dependency_check", "1");
      result._elem.SetAttribute("_keyed_name", result._itemRef.KeyedName);
      result._type = InstallType.DependencyCheck;
      return result;
    }
    public static InstallItem FromWarning(ItemReference itemRef, string warning)
    {
      var result = new InstallItem();
      result._itemRef = itemRef;
      result._name = warning;
      result._type = InstallType.Warning;
      return result;
    }

    internal static string RenderAttributes(XmlElement elem, string keyedName = null)
    {
      var builder = new StringBuilder();

      if (elem.HasAttribute("action"))
      {
        var action = elem.Attribute("action");
        builder.Append(char.ToUpper(action[0]))
          .Append(action.Substring(1))
          .Append(" of");
      }
      if (elem.HasAttribute("where"))
      {
        builder.Append(" ").Append(elem.Attribute("where"));
      }
      else
      {
        if (elem.HasAttribute("type"))
        {
          builder.Append(" ").Append(elem.Attribute("type")).Append(":");
        }
        if (!string.IsNullOrEmpty(keyedName))
        {
          builder.Append(" ").Append(keyedName);
        }
        else if (elem.HasAttribute("id"))
        {
          builder.Append(" ").Append(elem.Attribute("id"));
        }
      }

      if (elem.HasAttribute(XmlFlags.Attr_ScriptType))
      {
        builder.Append(" (").Append(elem.Attribute(XmlFlags.Attr_ScriptType)).Append(")");
      }

      return builder.ToString();
    }

    Stream IDiffFile.OpenRead()
    {
      var settings = new XmlWriterSettings();
      settings.OmitXmlDeclaration = true;
      settings.Indent = true;
      settings.IndentChars = "  ";
      settings.CloseOutput = false;

      var result = new MemoryStream();
      using (var writer = XmlTextWriter.Create(result, settings))
      {
        writer.WriteStartElement("AML");
        this.Script.WriteTo(writer);
        writer.WriteEndElement();
        writer.Flush();
      }
      result.Position = 0;
      return result;
    }
  }

  public static class InstallItemExtensions
  {
    public static string FilePath(this InstallItem line, HashSet<string> existingPaths, string extension = ".xml")
    {
      var folder = line.Type == InstallType.Script ? "_Scripts" : line.Reference.Type;
      var newPath = folder + "\\" + Utils.CleanFileName(line.Reference.KeyedName ?? line.Reference.Unique) + extension;
      if (existingPaths.Contains(newPath))
        newPath = folder + "\\" + Utils.CleanFileName((line.Reference.KeyedName ?? "") + "_" + line.Reference.Unique) + extension;
      return newPath;
    }
    public static void CleanKeyedNames(this IEnumerable<InstallItem> lines)
    {
      var existing = lines.Where(l => l.Type == InstallType.Create)
        .ToDictionary(l => l.Reference.Unique);
      InstallItem item;
      foreach (var line in lines.Where(l => l.Type == InstallType.Script))
      {
        if (existing.TryGetValue(line.InstalledId, out item))
        {
          line.Reference.KeyedName = InstallItem.RenderAttributes(line.Script, item.Reference.KeyedName);
        }
      }
    }
  }
}
