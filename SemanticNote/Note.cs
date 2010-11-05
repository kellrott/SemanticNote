/*
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License along
 * with this program; if not, write to the Free Software Foundation, Inc.,
 * 59 Temple Place - Suite 330, Boston, MA 02111-1307, USA.
 * http://www.gnu.org/copyleft/gpl.html
 */

using System;
using System.Collections;
using System.Collections.Generic;
using MindTouch.Deki.Script;
using MindTouch.Dream;
using MindTouch.Tasking;
using MindTouch.Deki;
using MindTouch.Xml;
using MindTouch.Deki.Script.Expr;
using System.Net;
using System.Text;
using System.IO;
using System.Web;
using System.Xml;
using System.Text.RegularExpressions;

using SemanticNote;
using MindTouch.Web;

namespace MindTouch.Deki.Services.Extension {
	using Yield = IEnumerator<IYield>;
	
	[DreamService("Semantic Note", "Copyright © 2010 University of California",
	    Info = "http://www.topsan.org/",
	    SID = new string[] { "sid://topsan.org/2010/07/extension/note" }
	)]
	[DreamServiceConfig("apikey", "string", "Apikey to access deki")]
	[DreamServiceConfig("wikiid", "string?", "Wiki instance to query (default: default)")]
	[DreamServiceConfig("uri.deki", "uri?", "Uri of deki (default: http://localhost:8081/deki)")]
	//[DreamServiceConfig("semantic-config-uri", "uri", "URI of configuration file")]
	[DreamServiceBlueprint("deki/service-type", "extension")]
	[DekiExtLibrary(
	    Label = "SemanticNote",
	    Namespace = "note",
	    Description = "Semantic Notation System"
	)]

	
	
	public class NoteExt : DekiExtService {			
		
		SemanticNoteConfig userConfig;
		string xsURI = "http://www.w3.org/2001/XMLSchema#";
		string mtURI = "http://mindtouch.com/schema#";
		SparqlInterface sparql;				
		string apikey;
		string plugName, plugPass;
		
		/*
		 * One startup, read the 'semantic-config-uri' parameter and load the configuration file
		 */
		protected override Yield Start(XDoc config, Result result) {
			Result res;
			yield return res = Coroutine.Invoke(base.Start, config, new Result());
			res.Confirm();
			// set location for storage service
			XUri configURI = config["semantic-config-uri"].AsUri;
			XUri sparqlURI = config[ "sparql-uri" ] .AsUri;
			string sparqlName = config[ "sparql-name" ].AsInnerText;
			string sparqlPass = config[ "sparql-pass" ].AsInnerText;
			apikey = config["apikey.deki"].AsText ?? string.Empty;
			plugName = config["plug-name"].AsText ?? string.Empty;
			plugPass = config["plug-pass"].AsText ?? string.Empty;

			sparql = new SparqlInterface( sparqlURI, sparqlName, sparqlPass );
			
			if ( configURI != null ) {
				Plug _deki = Plug.New(configURI);
				string xmlConfigStr = _deki.Get().ToString();
				XDoc doc = XDocFactory.From(xmlConfigStr, MimeType.XML);
				userConfig = new SemanticNoteConfig( doc["body"].Contents );
			}
					
	        result.Return();
	    }
		
		/*
		private string expandCurie(string curie) {
			string[] valList = curie.Split( ':' );
			if ( valList[0] == "http" ) {
				return curie;
			} else if ( valList[0] == "_" ) {
				return "#" + valList[1];
			} else if ( config.nsDict.ContainsKey( valList[0] ) ) {
				return config.nsDict[ valList[0] ] + valList[1];
			}
			return curie;
		}
		*/
		
		/*
		 * addRelData is an internal function that takes XDoc nodes and adds appropriate 
		 * 'rel', 'property', 'contents', 'datatype', and 'href' parameters to the node 
		 */
		private XDoc addRelData( XDoc node, string predicate, string value, string title, bool ?rev, bool ?visible ) {			
			string relUrl = userConfig.nsExpand( userConfig.defaultPredicateNS) + predicate;
			if ( userConfig.altDict.ContainsKey( predicate ) ) {
				relUrl = userConfig.nsExpand( userConfig.altDict[ predicate ] ) + predicate;	
			}			
			
			string valueString = null;
			string valueURL = null;
			string clickURL = null;
			if ( value.Contains(":") ) {
				string[] valList = value.Split( ':' );
				if ( valList[0] == "http" ) {
					valueURL = value;
					clickURL = valueURL;
				} else if ( valList[0] == "_" ) {
					valueURL = getDefaultSubject() + "#" + valList[1];
					clickURL = "#" + valList[1];
				} else if ( userConfig.nsDict.ContainsKey( valList[0].ToUpper() ) ) {
					valueURL = userConfig.nsDict[ valList[0].ToUpper() ] + valList[1];
					if ( userConfig.formatDict.ContainsKey( valList[0].ToUpper() ) ) {
						switch ( userConfig.formatDict[ valList[0].ToUpper() ] ) {
						case "toupper":
							valueURL = userConfig.nsDict[ valList[0].ToUpper() ] + valList[1].ToUpper();
							break;
						case "tolower":
							valueURL = userConfig.nsDict[ valList[0].ToUpper() ] + valList[1].ToLower();
							break;
						}
					}
					if ( userConfig.clickDict.ContainsKey( valList[0].ToUpper() ) ) {
						clickURL = userConfig.clickDict[ valList[0].ToUpper() ] + valList[1];
					} else {
						clickURL = valueURL;
					}
				} else {
					valueString = value;	
				}
			} else {
				valueString = value;
			}
			XDoc span;
			if ( valueURL != null ) {
				if ( rev != true )
					span = node.Attr("rel", relUrl).Attr("href", valueURL);
				else
					span = node.Attr("rev", relUrl).Attr("href", valueURL);
				if ( visible == null || visible == true )
					span = span.Start("a").Attr("href", clickURL );
			} else {
				span = node.Attr("property", relUrl);
				if ( visible==false )
					span = span.Attr("content", valueString);
				if ( userConfig.dataDict.ContainsKey( relUrl ) ) {
					span.Attr( "datatype", xsURI + userConfig.dataDict[ relUrl ] );	
				}
			}
			if ( visible == null || visible == true ) {
				if ( title == null ) 
					span.Value( value );
				else
					span.Value( title );

			}
			return span.End();
		}
		
		/*
		 * Inspect the current dream context and generate a default subject for 
		 * semantic information based on the 'page.id'.
		 */
		[DekiExtFunction("this", "Get Page Default Subject")]
		public string getDefaultSubject() {
			DekiScriptMap env = DreamContext.Current.GetState<DekiScriptMap>();
			if ( userConfig != null ) 
				return userConfig.subjectFromPageID( env.GetAt("page.id").AsString() );
			return env.GetAt("site.uri").AsString() + env.GetAt("page.path").AsString();
		}
				
		/*
		 * The 'link' function allows users to embed individual semantic links into 
		 * a page. It uses the configuration to lookup namepaces prefixes, and assign default
		 * namespaces.
		 * - rel : The type of relationship
		 * - value : The value assigned to the relationship, if it is a CURIE or a URI is 
		 * referenced with a 'href' in the 'span', otherwise it is referenced with a 'property'
		 * tag.
		 * - visible : Does the link produce visibile text
		 * - about : Has the user specified what the statement is about. By default it is
		 * the default subject namespace + the title of the page.
		 * - rev : Is the relationship reversed? (swaps the subject and the object by using the 
		 * 'rev' tag rather then the 'rel' tag), default 'false'
		 */
		[DekiExtFunction("link", "Semantic Link function")]
		public XDoc link (
						[DekiExtParam("rel")] string rel,
	        			[DekiExtParam("value")] string value,
	                    [DekiExtParam("visible", true)] bool ?visible,
                        [DekiExtParam("title", true)] string title,
	                    [DekiExtParam("about", true)] string about,
	                    [DekiExtParam("rev", true)] bool ?rev	                    
	                    )
		{		
			XDoc outVar =new XDoc("html");
			XDoc body = outVar.Start("body");
			string aboutURL;
			if ( about == null || about == "") {
				aboutURL = getDefaultSubject();
			} else {
				aboutURL = lookup(about);
			}
			addRelData( body.Start( "span" ).Attr("about", aboutURL), rel, value, title, rev, visible );
			return outVar;
		}
	
		string blankSubject() {
			Random r = new Random();
			int randID = r.Next(1000000);
			return getDefaultSubject() + "#" + randID.ToString("X");				
		}
		
		string getRelURL( string rel ) {
			string relUrl = userConfig.nsExpand( userConfig.defaultPredicateNS ) + rel;
			if ( userConfig.altDict.ContainsKey( rel ) ) {
				relUrl = userConfig.nsExpand( userConfig.altDict[ rel ] ) + rel;	
			}
			return relUrl;
		}
		
		void unWrapIdea( XDoc inNode, string rel, Object value, bool ?visible ) {
			string relUrl = getRelURL( rel );

			if ( visible == null )
				visible = false;
			if ( value is string ) {
				addRelData( inNode.Start("div").Attr("class", "ideaField"), rel, (string)value, null, false, visible );
			}
			if ( value is Hashtable ) {
				string blankNode = blankSubject();
				inNode.Start("div").Attr("rel", relUrl).Attr("href", blankNode).Attr("class", "ideaTable");          
				XDoc newNode = inNode.Start( "div" ).Attr("about", blankNode ).Attr("class", "ideaField");
				Hashtable tmp = (Hashtable)value;
				foreach ( string key in tmp.Keys ) {
					unWrapIdea( newNode, key, tmp[key], visible );
				}
			}
		}
		
		[DekiExtFunction("idea", "Compile Semantic Idea from Mapped Data")]
		public XDoc idea( 
						[DekiExtParam("value")] Hashtable value,
						[DekiExtParam("about", true)] string about,
		                [DekiExtParam("visible", true)] bool ?visible ) {
			XDoc outVar =new XDoc("html");
			XDoc body = outVar.Start("body");
			string aboutURL;
			
			if ( about == null || about == "") {
				aboutURL = getDefaultSubject();
			} else {
				aboutURL = about;
			}
			XDoc ideaNode = body.Start( "div" ).Attr("about", aboutURL ).Attr("class", "ideaTable");
			if ( visible == true ) {
				XDoc titleBar = ideaNode.Start("div").Attr("class", "ideaTitleBar");
				foreach ( string key in value.Keys ) {
					string relUrl = getRelURL( key );
					string label = getLabel( relUrl );
					titleBar.Start( "div" ).Attr("class", "ideaTitle").Value(label).End();
				}
				titleBar.End();
			}
			foreach ( string key in value.Keys ) {
				unWrapIdea( ideaNode, key, value[key], visible );
			}
			return outVar;	
		}
			
		
		/*
		 * Request a datasource to be imported into the sparql database. It works by adding
		 * an external_data link from the current page to the new graph URI, with the additional
		 * information. This can be picked up later by a bulk download system that is responsible 
		 * for updating the database.
		 * - name : Name of the graph set this belongs to. (may be depreciated..)
		 * - source : The URI of were to download source XML information. URI's with the '.gz'
		 * suffix are automatically decompressed. Otherwise data is assumed to be in XML.
		 * - xslt : The URI of the XML style sheet that will be used by xsltproc to convert the 
		 * source XML file into a RDF/XML file.
		 * - Graph URI : The URI this graph is associated with.
		 */
		[DekiExtFunction("import", "Import Function")]
		public XDoc import( 
		         [DekiExtParam("request")] string request
	    ) {
			XDoc outVar =new XDoc("html");
			XDoc a = outVar.Start("body");
			String subject = getDefaultSubject();
			
			string[] valList = request.Split( ':' );
			
			if ( !userConfig.importMap.ContainsKey( valList[0].ToUpper() ) ) {
				return outVar;	
			}
			
			foreach( SemanticNoteConfig.ImportMapping mapping in userConfig.importMap[ valList[0].ToUpper() ] ) {
				string graphURI = string.Format( mapping.graphURI, valList[1] );
				string source   = string.Format( mapping.sourceURI, valList[1] );
				string xslt     = mapping.xsltURI;
				a.Start("span").Attr("about",  subject ).Attr("rel", mtURI + "externalData").Attr("href", graphURI).End();
				a.Start("span").Attr("about", graphURI).Attr("rel", mtURI  + "importSource").Attr("href", source).End();
				if ( xslt != null ) {
					a.Start("span").Attr("about", source).Attr("rel", mtURI + "mappingXSLT").Attr("href", xslt).End();
					a.Start("span").Attr("about", source).Attr("rel", mtURI + "importType").Attr("href", mtURI + "xsltImport").End();
				} else {
					a.Start("span").Attr("about", source).Attr("rel", mtURI + "importType").Attr("href", mtURI + "rdfImport").End();			
				}
			}
			return outVar;		
		}
		
		/*
		 * lookup: Allows for user to query configured sparql server for data.
		 * - curie: the SPARQL formatted query.
		 * (return) : An array of hash for each row.
		 */
		[DekiExtFunction("lookup", "Change CURIE or URI to full URI")]
		public String lookup( [DekiExtParam("curie", true)] string curie ) {	
			if ( curie == null ) {
				return getDefaultSubject();
			}
			if ( curie.Contains(":") ) {
				string[] valList = curie.Split( ':' );
				if ( valList[0] == "http" ) {
					return curie;
				} else if ( valList[0] == "_" ) {
					return getDefaultSubject() + "#" + valList[1];
				} else if ( userConfig.nsDict.ContainsKey( valList[0].ToUpper() ) ) {
					return userConfig.nsDict[ valList[0].ToUpper() ] + valList[1];
				} 
			} 
			return curie;
		}
		
		/*
		 * lookup: Allows for user to query configured sparql server for data.
		 * - curie: the SPARQL formatted query.
		 * (return) : An array of hash for each row.
		 */
		[DekiExtFunction("lookupClick", "Change CURIE or URI to full Click link")]
		public String lookupClick( [DekiExtParam("curie")] string curie ) {			
			if ( curie.Contains(":") ) {
				string[] valList = curie.Split( ':' );
				if ( valList[0] == "http" ) {
					return curie;
				} else if ( valList[0] == "_" ) {
					return getDefaultSubject() + "#" + valList[1];
				} else if ( userConfig.clickDict.ContainsKey( valList[0].ToUpper() ) ) {
					return userConfig.clickDict[ valList[0].ToUpper() ] + valList[1];
				} else if ( userConfig.nsDict.ContainsKey( valList[0].ToUpper() ) ) {
					return userConfig.nsDict[ valList[0].ToUpper() ] + valList[1];
				} 
			} 
			return curie;
		}
		
		[DekiExtFunction("datatype", "Find datatype for predicate URI")]
		public String datatype( [DekiExtParam("uri")] string uri ) {
			if ( userConfig.dataDict.ContainsKey( uri.ToString() ) ) {
				return xsURI + userConfig.dataDict[ uri.ToString() ];	
			}
			return null;
		}
		
		String getLabel( string uri ) {
			if ( userConfig.labelDict.ContainsKey( uri.ToString() ) ) {
				return userConfig.labelDict[ uri.ToString() ];	
			}			
			string queryStr = "SELECT * WHERE { <" +  uri + "> <http://www.w3.org/2000/01/rdf-schema#label> ?label} ";
			
			Plug _deki = sparql.getSparqlPlug( queryStr, "xml" );
			
			string xmlConfigStr = _deki.Get().ToString();
			XDoc doc = XDocFactory.From(xmlConfigStr, MimeType.XML);
			ArrayList outArray = sparql.parseSparqlXML( doc["body"].Contents );	
			if ( outArray[0] != null ) {
				string labelStr = (string)((Hashtable)outArray[0])["label"];
				userConfig.labelDict.Add( uri, labelStr );
				return labelStr;
			}
			return null;
		}
		

		
		/*
		 * query: Allows for user to query configured sparql server for data.
		 * - query: the SPARQL formatted query.
		 * (return) : An array of hash for each row.
		 */
		[DekiExtFunction("query", "Sparql Query Function")]
		public ArrayList query( 
			[DekiExtParam("queryStr")] string queryStr ) {
			
			if ( sparql.sparqlType( queryStr ) == SparqlInterface.SparqlMethod.SELECT ) {
				Plug _deki = sparql.getSparqlPlug( queryStr, "xml" );
				string xmlConfigStr = _deki.Get().ToString();
				XDoc doc = XDocFactory.From(xmlConfigStr, MimeType.XML);
				return sparql.parseSparqlXML( doc["body"].Contents );	
			}
			return new ArrayList();
		}
		
		
		
		/*
		 * endpoint: Returns the URI for the SPARQL endpoint Proxy
		 */
		[DekiExtFunction("endpoint", "Sparql Query Function")]
		public string endpoint( ) {			
			return "/@api" + Self.At("sparql").Uri.Path;
			//return sparqlURI.ToString() + ":" + sparqlName + ":" + sparqlPass;
		}
			
		/*
		string [,]deHTML = {  {"&Aring;", "A"}, {"&beta;", "B"}, {"&auml;", "A"}, {"(&rsquo;|&lsquo;)", "'"}, {"(&rdquo;|&ldquo;)", "\""},
			{"&nbsp;", " "}, {"&ndash;", "-"}, {"&alpha;", "a"}, {"&epsilon;", "E"}, {"&zeta;", "Z"}, {"&gamma;", "G" }, {"&copy;", "(C)"},
			{"&szlig;", "B"}, {"&deg;", "o"}, {"&larr;", "<-"}, {"&rarr;", "->"}, {"&uarr;", "^"}, {"&darr;", "v"} , {"&harr;", "<->"},
			{"&dagger;", "t"}, {"&prime;", "`"}, {"&Prime;", "``"}, {"&minus;", "-"}, {"&ouml", "o"}, {"&Ouml", "O"}, {"&eacute;", "E"},
			{"&uuml;", "U"}, {"&hellip;", "..."}, {"&oacute;", "O"}, {"&aacute;", "A"}
		};
		*/
		
		string [,]toHTML = { {"&amp;", "&amp;amp;" }, {"&gt;", "&amp;gt;" }, {"&lt;", "&amp;lt;" } };
		
		
		Plug getAuthPlug( XUri uri ) {
			Plug _deki = Plug.New( uri ); 

			if (apikey != string.Empty ) 
				_deki = _deki.With("apikey", apikey );
			
			if ( plugPass != string.Empty && plugName != string.Empty )
				_deki = _deki.WithCredentials( plugName, plugPass );
			
			return _deki;
		}
		
		string getPageRDFa( DreamContext context, DreamMessage request, string pageID ) {
			XUri pageList = new XUri( context.ServerUri ).At( "deki","pages",pageID,"contents");			
			Plug _deki = getAuthPlug( pageList ); 
					
			string xmlString = _deki.Get().ToText();
			XmlDocument xdoc = new XmlDocument();
			xdoc.LoadXml( xmlString );
			//outDoc.Value( xdoc.DocumentElement.InnerText );
			string additionalInfo = "";
			if ( userConfig != null ) {
				additionalInfo += "<span about='" + userConfig.subjectFromPageID( pageID ) + "'>" + 
				"<span property='" + mtURI + "pageID" + "' content='" + pageID + "'/>;";
				
				XUri pageInfo = new XUri( context.ServerUri ).At( "deki","pages",pageID );			
				Plug _infoDeki = getAuthPlug( pageInfo );
				
				XDoc infoDoc = _infoDeki.Get().ToDocument();
				
				additionalInfo += "<span property='" + mtURI + "pagePath" + "' content='" + infoDoc["path"].Contents + "'/>";
				//additionalInfo += "<span rel='" + mtURI + "pageURI" + "' href='" + infoDoc["uri.ui"].Contents + "'/>";
				additionalInfo += "<span property='" + mtURI + "pageRevision" + "' content='" + infoDoc["@revision"].Contents + "'/>";
				if ( userConfig.pageAliasPredicate != null ) {
					additionalInfo += "<span property='" + userConfig.curieExpand( userConfig.pageAliasPredicate ) + "' content='" + 
						userConfig.aliasFromPageID( pageID ) + "'/>";
					//additionalInfo += "<span property='" + userConfig.curieExpand( userConfig.pageAliasPredicate ) + "' content='" + 
					//	infoDoc["path"].Contents + "'/>";
				}
				XDoc filesNode = infoDoc["files"];
				if ( !filesNode.IsEmpty ) {
					XDoc fileNode = filesNode["file"];
					while ( !fileNode.IsEmpty ) {
						string fileURL = fileNode["contents"]["@href"].Contents;
						if ( fileURL.EndsWith(".rdf") ) {
							additionalInfo += "<span rel='" + mtURI + "externalData' href='" + fileURL +"'/>";
							additionalInfo += "<span about='" + fileURL + "' rel='" + mtURI + "importSource' href='" + fileURL +"'/>";
							additionalInfo += "<span about='" + fileURL + "' property='" + mtURI + "pageRevision' content='" + fileNode["@revision"].Contents +"'/>";
							additionalInfo += "<span about='" + fileURL + "' rel='" + mtURI + "importType' href='" + mtURI + "attachedRDF'/>";
							
						}
						fileNode = fileNode.Next;
					}
				}
				//"<span property='" + mtURI + "pageID" + "' content='" + pageID + "'/>" +
				additionalInfo += "</span>";
			}
				
			string outStr = "<html version=\"XHTML+RDFa 1.0\" ><body>" + xdoc.DocumentElement.InnerText + additionalInfo + "</body></html>";
			for ( int i = 0; i < toHTML.GetLength(0); i++ ) {
				outStr = Regex.Replace( outStr, toHTML[i,0], toHTML[i,1] );
			}			
			outStr = HttpUtility.HtmlDecode( outStr );			
			return outStr;
		}
		
		[DreamFeature("GET:rdfa", "Service to get RDFa content with no extra HTML from skin")]
		[DreamFeatureParam("id", "string?", "Page ID")]
		public Yield rdfa(DreamContext context, DreamMessage request, Result<DreamMessage> response) {	
			string aliasStr = context.GetParam("id", string.Empty);
			
			if ( aliasStr == string.Empty ) {
				//XUri pageList = new XUri( context.ServerUri ).At( "deki","pages");			
				//Plug _deki = Plug.New( pageList ); 				
				//XmlDocument xdoc = new XmlDocument();
				//xdoc.Load(  _deki.Get().ToStream()  );		
				StringBuilder sb = new StringBuilder();
				sb.Append("<html version=\"XHTML+RDFa 1.0\" ><body>");
				
				//foreach ( XmlNode node in xdoc.DocumentElement.SelectNodes("descendant::page") ) {
				//	XmlNode pathNode = node.SelectSingleNode("./path");
			//		if ( pathNode.InnerText.Length > 0 ) {
			//			sb.Append( "<div rel='seeAlso'><a href='rdfa?alias=" +  node.Attributes["id"].Value + "'>" + pathNode.InnerText + "</a></div>" );
			//		}
			//	}	
				sb.Append("</body></html>");
				response.Return( DreamMessage.Ok( MimeType.HTML, sb.ToString() ) );
			} else {
				string pageID = aliasStr;
				string outStr = getPageRDFa( context, request, pageID );				
				response.Return( DreamMessage.Ok( MimeType.HTML, outStr ) );
			}
			yield break;	
		}		
		
		[DreamFeature("GET:rdf", "Service to get RDF translations of pages")]
		[DreamFeatureParam("id", "string?", "Page ID")]
		public Yield rdf(DreamContext context, DreamMessage request, Result<DreamMessage> response) {
			string aliasStr = context.GetParam("id", string.Empty);
			if ( aliasStr == string.Empty ) {
				//XUri pageList = new XUri( context.ServerUri ).At( "deki","pages");			
				//Plug _deki = Plug.New( pageList ); 				
				//XmlDocument xdoc = new XmlDocument();
				//xdoc.Load(  _deki.Get().ToStream()  );		
				StringBuilder sb = new StringBuilder();
				sb.Append("<?xml version=\"1.0\"?><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\" xmlns:owl=\"http://www.w3.org/2002/07/owl#\">");
				
				//sb.Append("<rdf:Description rdf:about=\"\">"); 
				//foreach ( XmlNode node in xdoc.DocumentElement.SelectNodes("descendant::page") ) {
			//		XmlNode pathNode = node.SelectSingleNode("./path");
			//		if ( pathNode.InnerText.Length > 0 ) {
			//			sb.Append( "<owl:seeAlso rdf:resource='rdf?alias=" +  node.Attributes["id"].Value + "'/>" );
			//		}
			//	}	
			//	sb.Append("</rdf:Description>");
				
				sb.Append("</rdf:RDF>");
				response.Return( DreamMessage.Ok( MimeType.XML, sb.ToString() ) );
			} else {
				string pageID = aliasStr;
				string outStr = getPageRDFa( context, request, pageID );	
				
				XmlDocument xdoc2 = new XmlDocument();
				xdoc2.LoadXml( outStr );
				
				RDFa2RDF trans = new RDFa2RDF();
				if ( userConfig != null )  // why is this null?
					trans.SetNameSpaces( userConfig.nsDict );
				trans.Scan( xdoc2.DocumentElement );
				response.Return( DreamMessage.Ok( MimeType.XML, new XDoc(trans.toXML()) ) );
				yield break;
			}
		}	
		
		const string AUTHTOKEN_URIPARAM = "authtoken";
        const string AUTHTOKEN_COOKIENAME = "authtoken";
        const string AUTHTOKEN_HEADERNAME = "X-Authtoken";
		
		Regex reUpdate = new Regex("UPDATE");
		Regex reCreate = new Regex("CREATE");
		Regex reDelete = new Regex("DELETE");
		
		[DreamFeature("GET:sparql", "Deki Sparql Proxy for AJAX calls")]
		[DreamFeatureParam("query", "string?", "SPARQL QUERY")]
		public Yield sparqlProxy(DreamContext context, DreamMessage request, Result<DreamMessage> response) {									
			
			//string outStr = "nothing :" + this.userConfig + ":" + this.sparql;
			//if ( userConfig != null ) {
			//	outStr = userConfig.defaultSubjectNS;
			//}
			//response.Return( DreamMessage.Ok( MimeType.TEXT, outStr) );
			
			string queryStr = context.GetParam("query", string.Empty);
			
			SparqlInterface.SparqlMethod qType = sparql.sparqlType( queryStr );
			
			bool allowed = false;
			if ( qType == SparqlInterface.SparqlMethod.SELECT ) {
				allowed = true;
			} else {			
				//DekiScriptMap env = DreamContext.Current.GetState<DekiScriptMap>();
				string[] sparqlDest = sparql.sparqlDestGraph( queryStr );
				allowed = true;
				bool found = false;
				foreach (string dstGraph in sparqlDest ) {					
					string baseGraph = dstGraph.Substring( 0, dstGraph.LastIndexOf('.') );					
					if ( baseGraph.Length > 1 && baseGraph.CompareTo( dstGraph ) != 0 ) {
						string graphQuery = "PREFIX mind:<http://mindtouch.com/schema#> SELECT ?pageID FROM <{0}> WHERE {{ ?page mind:pageID ?pageID }}";
						string iQuery = string.Format( graphQuery, baseGraph );
						Plug _deki = sparql.getSparqlPlug( iQuery, "xml" );
						string xmlConfigStr = _deki.Get().ToString();
						XDoc doc = XDocFactory.From(xmlConfigStr, MimeType.XML);
						foreach (Object row in sparql.parseSparqlXML( doc["body"].Contents ) ) {
							Hashtable h = (Hashtable)row;
							string pageID = (string) h[ "pageID" ];						
							XUri secPage = new XUri( context.ServerUri ).At( "deki","pages",pageID,"security");
							Plug authedPlug = Plug.New( secPage );
							string authToken = request.Headers[AUTHTOKEN_HEADERNAME];
			            	if(string.IsNullOrEmpty(authToken)) {
			                	DreamCookie cookie = DreamCookie.GetCookie(request.Cookies, AUTHTOKEN_COOKIENAME);
	    		            	if(cookie != null) {
	        		        	    authedPlug = authedPlug.WithHeader(AUTHTOKEN_HEADERNAME, cookie.Value);
	        		       		}
					        }
		    		        string userName, password;
	    	    		    HttpUtil.GetAuthentication(context.Uri.ToUri(), request.Headers, out userName, out password);
	        			    if(!string.IsNullOrEmpty(userName) || !string.IsNullOrEmpty(password)) {
	        		    	    authedPlug = authedPlug.WithCredentials(userName, password);
	        		   		}
							string perms = authedPlug.Get().ToDocument()["permissions.effective"].AsInnerText;
							if ( !reCreate.IsMatch( perms ) || !reDelete.IsMatch( perms ) || !reUpdate.IsMatch( perms ) ) 
								allowed = false;
							found = true;	
							//response.Return( DreamMessage.Ok( MimeType.TEXT, "GRAPHS " +  dstGraph + "=" + allowed ) );
							//yield break;
						}
						//response.Return( DreamMessage.Ok( MimeType.TEXT, "QUERY " +  iQuery + "=" + allowed ) );
						//yield break;
					}					
				}
				if ( !found )
					allowed = false;
				//response.Return( DreamMessage.Ok( MimeType.TEXT, "GRAPHS " + string.Join(";", sparqlDest ) + "=" + allowed ) );
				//yield break;
			}
			
			if ( allowed ) {
				Plug _deki = sparql.getSparqlPlug( queryStr, "json" );
				string outStr = _deki.Get().ToText(); //+ doc["message"]["body"].Contents;
				response.Return( DreamMessage.Ok( MimeType.JSON, outStr) );
			} else {
				response.Return( DreamMessage.AccessDenied("DEKI", DekiResources.AUTHENTICATION_FAILED));
			}
			yield break;
		}	
		
	}
	
	
	/*
	 * 
	 * 
	 * 
	 */
	
	
	public class SparqlInterface {
		
		XUri sparqlURI = null;
		string sparqlName = null;
		string sparqlPass = null;
		
		public SparqlInterface( XUri sparqlURI, string sparqlName, string sparqlPass ) {
			this.sparqlURI = sparqlURI;
			this.sparqlName = sparqlName;
			this.sparqlPass = sparqlPass;
		}
			
		public enum SparqlMethod {
			UNKNOWN,
			SELECT,
			UPDATE			
		};
		Regex selectRE = new Regex("^[^{]*SELECT.*WHERE.*{.*}.*$", RegexOptions.IgnoreCase | RegexOptions.Singleline );
		Regex askRE = new Regex("^[^{]*ASK.*{.*}.*$", RegexOptions.IgnoreCase | RegexOptions.Singleline );
		Regex insertRE = new Regex("^[^{]*INSERT.*(INTO|DATA).*{.*}.*$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
		Regex deleteRE = new Regex("^[^{]*DELETE.*DATA.*{.*}.*$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
		Regex clearRE = new Regex("^[^{]*CLEAR.*GRAPH.*", RegexOptions.IgnoreCase | RegexOptions.Singleline);
		public SparqlMethod sparqlType(string query) {
			if ( insertRE.IsMatch( query ) )
				return SparqlMethod.UPDATE;
			if ( deleteRE.IsMatch( query ) )
				return SparqlMethod.UPDATE;
			if ( selectRE.IsMatch( query ) )
				return SparqlMethod.SELECT;
			if ( askRE.IsMatch( query ) )
				return SparqlMethod.SELECT;			
			if ( clearRE.IsMatch( query ) )
				return SparqlMethod.UPDATE;			
			return SparqlMethod.UNKNOWN;
		}
		
		Regex insertIntoRE = new Regex("INSERT.*INTO *<([^>]*)> *{.*}", RegexOptions.IgnoreCase | RegexOptions.Singleline);
		Regex deleteFromRE = new Regex("DELETE.*FROM.*<([^>]*)>.*{.*}", RegexOptions.IgnoreCase | RegexOptions.Singleline);
		Regex clearFromRE =  new Regex("CLEAR.*GRAPH.*<([^>]*)>.*{.*}", RegexOptions.IgnoreCase | RegexOptions.Singleline);

		public string[] sparqlDestGraph(string query) {
			List<string> outList = new List<string>();			
			Match hit;			
			hit = insertIntoRE.Match(query);
			if ( hit != null ) {
				for ( int i = 1; i < hit.Groups.Count; i++ ) {
					if ( hit.Groups[i].Captures.Count > 0 ){
						//Console.Out.WriteLine( "en:" + i + hit.Groups[i].Captures[0].Value );
						outList.Add( hit.Groups[i].Captures[0].Value );
					}
				}
			}			
			hit = deleteFromRE.Match(query);
			if ( hit != null ) {
				for ( int i = 1; i < hit.Groups.Count; i++ ) {
					if ( hit.Groups[i].Captures.Count > 0 ){
						//Console.Out.WriteLine( "en:" + i + hit.Groups[i].Captures[0].Value );
						outList.Add( hit.Groups[i].Captures[0].Value );
					}
				}
			}
			hit = clearFromRE.Match(query);
			if ( hit != null ) {
				for ( int i = 1; i < hit.Groups.Count; i++ ) {
					if ( hit.Groups[i].Captures.Count > 0 ){
						//Console.Out.WriteLine( "en:" + i + hit.Groups[i].Captures[0].Value );
						outList.Add( hit.Groups[i].Captures[0].Value );
					}
				}
			}
			return outList.ToArray();
		}
		
		public ArrayList parseSparqlXML( string xmlString ) {					
			XmlDocument xdoc = new XmlDocument();
			xdoc.LoadXml( xmlString );
			ArrayList outArray = new ArrayList();
			foreach ( XmlNode a in xdoc.GetElementsByTagName( "result" ) ) {
				Hashtable row = new Hashtable();
				foreach ( XmlNode b in a.ChildNodes ) {
					string name = b.Attributes["name"].Value;
					if ( b.FirstChild.Name == "uri" ) {
						//Uri val = new Uri( b.FirstChild.InnerText );
						string val = b.FirstChild.InnerText;
						row.Add( name, val );
					} else if ( b.FirstChild.Name == "literal" ) {
						//string datatype = b.FirstChild.Attributes[ "datatype" ].Value;
						string val = b.FirstChild.InnerText;
						row.Add( name, val );
						/*
						if ( datatype != null ) {
							datatype = datatype.Replace("http://www.w3.org/2001/XMLSchema#", "");
							switch (datatype) {
							case "integer":
								Int32 val = Int32.Parse( b.FirstChild.InnerText );
								row.Add( name, val );
								break;
							case "float":
								float floatVal = float.Parse( b.FirstChild.InnerText );
								row.Add( name, floatVal );
								break;
							}				
						}
						*/
					}
				}
				outArray.Add( row );
			}
			return outArray;
		}
		
		public Plug getSparqlPlug(string query, string output) {
			string queryURL = sparqlURI.ToString() + "?output=" + output + "&query="  + HttpUtility.UrlEncode(query);						
			Plug _deki = Plug.New( queryURL, new TimeSpan(0,0,15) );			
			if ( sparqlName != null && sparqlPass != null ) {	
				_deki = _deki.WithCredentials(sparqlName, sparqlPass );	
				//string auth = sparqlName + ":" + sparqlPass;
				//string encoded = Convert.ToBase64String( Encoding.UTF8.GetBytes( auth ) );
				//_deki = _deki.WithHeader( "Authorization", "Basic " + encoded ); 
			}	
			return _deki;
		}
	}


	public class SemanticNoteConfig
	{		
		public Dictionary<string, string> nsDict;	
		public Dictionary<string, string> clickDict;
		public Dictionary<string, string> formatDict;
		public Dictionary<string, string> altDict;
		public Dictionary<string, string> dataDict;
		public Dictionary<string, string> labelDict;
		public Dictionary<string, List<ImportMapping> > importMap;
		
		
		public class ImportMapping {
			public string sourceURI, xsltURI, graphURI;		
			public ImportMapping( string sourceURI, string graphURI, string xsltURI ) {
				this.sourceURI = sourceURI;
				this.graphURI = graphURI;
				if ( xsltURI != null )
					this.xsltURI = xsltURI;
			}
		}
		
		public string defaultPredicateNS, defaultSubjectNS, sparqlServer;
		public string pageAlias, pageAliasPredicate;
		
		public SemanticNoteConfig( Stream inStream) {
			XmlDocument xdoc = new XmlDocument();
			xdoc.Load( inStream );
			this.ParseConfigXML( xdoc );
		}
		
		public SemanticNoteConfig (string xmlString) {
			//XmlTextReader reader = new XmlTextReader(new System.IO.StringReader(xmlString));
			XmlDocument xdoc = new XmlDocument();
			xdoc.LoadXml( xmlString );
			this.ParseConfigXML( xdoc );
		}		
		
		public void ParseConfigXML( XmlDocument xdoc ) {
			XmlNode root = xdoc.DocumentElement;			

			Regex xmlns = new Regex("^xmlns:");

			nsDict = new Dictionary<string, string>();
			clickDict = new Dictionary<string, string>();
			formatDict = new Dictionary<string, string>();
			altDict = new Dictionary<string, string>();
			dataDict = new Dictionary<string, string>();
			labelDict = new Dictionary<string, string>();
			importMap = new Dictionary<string, List<SemanticNoteConfig.ImportMapping> >();
			
			for ( int i = 0 ; i < root.Attributes.Count; i++ ) {
				if ( xmlns.IsMatch( root.Attributes[i].Name ) ) {
					string ns = xmlns.Replace( root.Attributes[i].Name, "" );
					nsDict.Add( ns.ToUpper(), root.Attributes[i].Value );
					//Console.Out.WriteLine( ns + " : " + root.Attributes[i].Value );
				}
			}			
			//Console.Out.WriteLine( root.Attributes[0].Name );			
			foreach ( XmlNode a in root.ChildNodes ) {
				switch (a.Name) {
				case "defaultPredicateNS":
					parseDefaultPredicateNS( a );
					break;
				case "defaultSubjectNS":
					parseDefaultSubjectNS( a );
					break;
				case "sparql":
					parseSparql( a );
					break;
				case "clickMap":
					parseClickMap( a );
					break;
				case "altNS":
					parseAltNS( a );
					break;
				case "dataTypes":
					parseDataTypes( a );
					break;
				case "pageAlias":
					parsePageAlias( a );
					break;
				case "aliasPredicate":
					parseAliasPredicate( a );
					break;
				case "importMap":
					parseImportMap( a );
					break;
				case "format":
					parseFormat( a );
					break;
				}
			}			
		}
		
		private void parseDefaultPredicateNS( XmlNode node ) {
			this.defaultPredicateNS = node.InnerText;	
		}
		
		private void parseDefaultSubjectNS( XmlNode node ) {
			this.defaultSubjectNS = node.InnerText;	
		}
		
		private void parseSparql( XmlNode node ) {
			this.sparqlServer = node.InnerText;	
		}
		
		private void parseClickMap( XmlNode node ) {
			string clickLink = node.InnerText;
			string ns = node.Attributes["ns"].Value;
			clickDict.Add( ns.ToUpper(), clickLink );
		}

		private void parseFormat( XmlNode node ) {
			string format = node.InnerText;
			string ns = node.Attributes["ns"].Value;
			formatDict.Add(  ns.ToUpper(), format );
		}
		
		private void parseAltNS( XmlNode node ) {
			foreach ( XmlNode a in node.ChildNodes ) {				
				altDict.Add( a.Name, a.InnerText );
			}
		}
		
		private void parseImportMap( XmlNode node ) {
			foreach ( XmlNode a in node.ChildNodes ) {
				String name = a.Name;

				List<ImportMapping> importList = new List<ImportMapping>();
				
				foreach (XmlNode curSource in a.SelectNodes("source") ) {
					String graph = curSource.SelectSingleNode( "graph" ).InnerText;
					String sourceUrl = curSource.SelectSingleNode("url").InnerText;
					string xslt = null;
					if ( curSource.SelectSingleNode("xslt") != null )
						xslt = curSource.SelectSingleNode( "xslt" ).InnerText;					
					importList.Add( new ImportMapping( sourceUrl, graph, xslt ) );
				}
				importMap.Add( name.ToUpper(), importList );
			}
		}
		
		private void parseDataTypes( XmlNode node ) {
			foreach ( XmlNode a in node.ChildNodes ) {				
				dataDict.Add(  a.NamespaceURI + a.LocalName, a.InnerText );
			}
		}
		
		private void parsePageAlias( XmlNode node ) {
			this.pageAlias = node.InnerText;	
		}		
		
		private void parseAliasPredicate( XmlNode node ) {
			this.pageAliasPredicate = node.InnerText;	
		}	
		
		
		/*
		 * Function to generate subject string based on page.id.
		 */
		public string subjectFromPageID( string pageID ) {
			return nsDict[ defaultSubjectNS.ToUpper() ] + aliasFromPageID(pageID);
		}

		public string curieExpand( string curie ) {
			if ( curie.Contains(":") ) {
				string[] valList = curie.Split( ':' );
				if ( valList[0] == "http" ) {
					return curie;
				} else if ( nsDict.ContainsKey( valList[0].ToUpper() ) ) {
					return nsDict[ valList[0].ToUpper() ] + valList[1];
				} 
			} 
			return curie;	
		}
		
		public string aliasFromPageID( string pageID ) {
			return string.Format( this.pageAlias, pageID );
		}
		
		public string nsExpand( string ns ) {
			if ( nsDict.ContainsKey( ns.ToUpper() ) ) {
				return nsDict[ ns.ToUpper() ];
			}
			return ns;
		}
	}
}