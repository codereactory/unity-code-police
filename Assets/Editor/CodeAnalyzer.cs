using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityCodePolice.Editor
{
	public sealed class CodeAnalyzer : AssetPostprocessor
	{
		public override int GetPostprocessOrder()
		{
			return 20;
		}

		[MenuItem("Debug/Add Stylecop")]
		public static void OnGeneratedCSProjectFiles()
		{
			try
			{
				var lines = GetSolutionProjectReferences();

				var projectFiles = Directory
					.GetFiles(Directory.GetCurrentDirectory(), "*.csproj")
					.Where(
						csprojFile =>
							lines.Any(line => line.Contains("\"" + Path.GetFileName(csprojFile) + "\"")))
					.ToArray();

				foreach (var file in projectFiles)
				{
					UpdateProjectFile(file);
				}
			}
			catch (Exception e)
			{
				// unhandled exception kills editor
				Debug.LogError(e);
			}
		}

		private static string[] GetSolutionProjectReferences()
		{
			var projectDirectory = Directory.GetParent(Application.dataPath).FullName;
			var projectName = Path.GetFileName(projectDirectory);
			var slnFile = Path.GetFullPath(string.Format("{0}.sln", projectName));

			if (!File.Exists(slnFile))
			{
				return new string[0];
			}

			var text = File.ReadAllText(slnFile);
			var lines = text
				.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
				.Where(a => a.StartsWith("Project(")).ToArray();

			return lines;
		}

		private static void UpdateProjectFile(string projectFile)
		{
			XDocument doc;
			try
			{
				doc = XDocument.Load(projectFile);
			}
			catch (Exception)
			{
				Debug.LogError(string.Format("Failed to parse {0}", projectFile));
				return;
			}

			var projectContentElement = doc.Root;
			XNamespace xmlns = projectContentElement.Name.NamespaceName;
			SetRoslynAnalyzers(projectContentElement, xmlns);

			doc.Save(projectFile);
		}

		private static void SetRoslynAnalyzers(
			XElement projectContentElement, XNamespace xmlns)
		{
			var currentDirectory = Directory.GetCurrentDirectory();
			var currentDirectoryInfo = new DirectoryInfo(currentDirectory);

			var files = currentDirectoryInfo
					.GetFiles("*", SearchOption.TopDirectoryOnly)
					.Select(x => new FileInfo(x.FullName.Substring(currentDirectory.Length + 1)));

			var rulesetFile = files.FirstOrDefault(x => x.Extension == ".ruleset");
			if (rulesetFile != null)
			{
				SetOrUpdateProperty(projectContentElement, xmlns, "CodeAnalysisRuleSet", rulesetFile.FullName);
			}

			var itemGroup = new XElement(xmlns + "ItemGroup");

			var jsonSpecification = files.FirstOrDefault(x => x.Extension == ".json");
			if (jsonSpecification != null)
			{
				var stylecopJson = new XElement(xmlns + "AdditionalFiles", new XAttribute("Include", "stylecop.json"));
				itemGroup.Add(stylecopJson);
			}

			var pkg = new XElement("PackageReference", new XAttribute("Include", "StyleCop.Analyzers"));
			pkg.Add(new XElement("Version", "1.1.118"));
			pkg.Add(new XElement("IncludeAssets", "runtime; build; native; contentfiles; analyzers"));
			pkg.Add(new XElement("PrivateAssets", "all"));

			itemGroup.Add(pkg);

			projectContentElement.Add(itemGroup);
		}

		private static void SetOrUpdateProperty(
			XElement root, XNamespace xmlns, string elementName, string fileName)
		{
			var element = root
					.Elements(xmlns + "PropertyGroup")
					.Elements(xmlns + elementName)
					.FirstOrDefault();

			if (element != null)
			{
				if (element.Value != fileName)
				{
					element.SetValue(fileName);
				}
			}
			else
			{
				AddProperty(root, xmlns, elementName, fileName);
			}
		}

		private static void AddProperty(XElement root, XNamespace xmlns, string name, string content)
		{
			var propertyGroup = root
				.Elements(xmlns + "PropertyGroup")
				.FirstOrDefault(e => !e.Attributes(xmlns + "Condition").Any());

			if (propertyGroup == null)
			{
				propertyGroup = new XElement(xmlns + "PropertyGroup");
				root.AddFirst(propertyGroup);
			}

			propertyGroup.Add(new XElement(xmlns + name, content));
		}
	}
}
