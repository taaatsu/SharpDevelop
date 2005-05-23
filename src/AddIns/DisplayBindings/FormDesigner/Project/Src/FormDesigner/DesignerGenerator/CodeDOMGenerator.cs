// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krger" email="mike@icsharpcode.net"/>
//     <version value="$version"/>
// </file>

using System;
using System.IO;
using System.Xml;
using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.ComponentModel.Design.Serialization;
using System.Text;
using System.Text.RegularExpressions;

using System.CodeDom;
using System.CodeDom.Compiler;

using Microsoft.CSharp;
using Microsoft.VisualBasic;
using ICSharpCode.Core;
using ICSharpCode.FormDesigner.Services;


namespace ICSharpCode.FormDesigner
{
	/// <summary>
	/// This class is able to generate a CodeDOM definition out of a XML file.
	/// </summary>
	public class CodeDOMGenerator
	{
		IDesignerHost   host;
		CodeDomProvider      codeProvider;
		
		CodeDOMGeneratorUtility codeDOMGeneratorUtility = new CodeDOMGeneratorUtility();
		
		public CodeDOMGenerator(IDesignerHost host, CodeDomProvider codeProvider)
		{
			this.host = host;
			this.codeProvider = codeProvider;
		}
		
		public void ConvertContentDefinition(TextWriter writer)
		{
			DesignerSerializationManager serializationManager = (DesignerSerializationManager)host.GetService(typeof(IDesignerSerializationManager));
			IDisposable session = serializationManager.CreateSession();
			DesignerResourceService designerResourceService = (DesignerResourceService)host.GetService(typeof(System.ComponentModel.Design.IResourceService));
			designerResourceService.SerializationStarted(true);
			
			Type componentType = host.RootComponent.GetType();
			ExpressionContext exprContext = new ExpressionContext(new CodeThisReferenceExpression(), componentType, host.RootComponent, host.RootComponent);
			((IDesignerSerializationManager)serializationManager).Context.Append(exprContext);
			
			CodeDomSerializer rootSerializer = (CodeDomSerializer)serializationManager.GetSerializer(componentType, typeof(CodeDomSerializer));
			
			if (rootSerializer == null) {
				throw new Exception("No root serializer found");
			}
			
			ICollection statements = rootSerializer.Serialize(serializationManager, host.RootComponent) as ICollection;
			CodeGeneratorOptions options = codeDOMGeneratorUtility.CreateCodeGeneratorOptions;
			options.IndentString = "\t\t\t";
			
			foreach (CodeStatement statement in statements) {
				if (!(statement is CodeVariableDeclarationStatement)) {
					// indentation isn't generated when calling GenerateCodeFromStatement
					writer.Write(options.IndentString);
					try {
						codeProvider.GenerateCodeFromStatement(statement, writer, options);
					} catch (Exception e) {
						codeProvider.GenerateCodeFromStatement(new CodeCommentStatement("TODO: Error while generating statement : " + e.Message),
						                                       writer,
						                                       options);
					}
				}
			}
			designerResourceService.SerializationEnded(true);
			session.Dispose();
		}
	}
}
