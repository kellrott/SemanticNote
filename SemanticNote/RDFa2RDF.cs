
using System;
using System.IO;
using System.Xml;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Schema;


namespace SemanticNote
{
	public class RDFa2RDF
	{
		
		private ArrayList aboutStack;
		private Dictionary<string, List<PredPair> > aboutTable;
		private Dictionary<string, string> nsTable;
		private	string rdfNS =  "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
		public RDFa2RDF () {
			aboutStack = new ArrayList();
			aboutTable = new Dictionary<string, List<PredPair>>();
			nsTable = new Dictionary<string, string>();
			nsTable.Add( rdfNS, "rdf" );
		}
		
		public void SetNameSpaces(Dictionary<string, string> nsIn) {
			if ( nsIn != null ) {
				foreach ( string ns in nsIn.Keys ) {
					if ( !nsTable.ContainsKey( nsIn[ns] ) ){
						nsTable.Add( nsIn[ns], ns );	
					}
				}
			}
		}
		
		class PredPair {
			public string predicate;
			public string href;
			public string content;
			public string datatype;
		}

		char []endChars = { '#', '/' };

		public void Scan( XmlNode node ) {
			//Console.Out.WriteLine( node.Name );
			if ( node.Attributes != null ) {
				if ( node.Attributes["about"] != null ) {
					//Console.Out.WriteLine( "ABOUT: " + node.Attributes["about"].Value );	
					aboutStack.Add( node.Attributes["about"].Value );
				}				
				if ( aboutStack.Count > 0 && (node.Attributes["rel"] != null || node.Attributes["rev"] != null) && node.Attributes["href"] != null ) {
					string predicate;
					bool rev = false;
					if ( node.Attributes["rel"] != null ) {
						predicate = node.Attributes["rel"].Value;
					} else {
						predicate = node.Attributes["rev"].Value;
						rev = true;
					}
					if ( predicate.CompareTo( "internal" ) != 0 && predicate.CompareTo("external nofollow") != 0 ) {
						string href = node.Attributes["href"].Value; 					
						PredPair p = new PredPair();
						p.predicate = predicate;
						string subject;
						if ( rev ) { 
							p.href = (string)aboutStack[ aboutStack.Count - 1];
							subject = href;
						} else {
							p.href = href;
							subject = (string)aboutStack[ aboutStack.Count - 1];
						}
						
						if ( aboutTable.ContainsKey( subject ) ) {
							aboutTable[ subject ].Add( p );
						} else { 
							List<PredPair> tmp = new List<PredPair>();
							tmp.Add( p );
							aboutTable.Add( subject, tmp );
						}
						//Console.Out.WriteLine( "<" + aboutStack[ aboutStack.Count - 1] + "> <" + predicate + "> <" + href + ">" );
					}
				}
				if ( node.Attributes["property"] != null ) {					
					PredPair p = new PredPair();					

					string subject = (string)aboutStack[ aboutStack.Count - 1];
					p.predicate = node.Attributes["property"].Value;
					if ( node.Attributes["content"] != null ) {
						p.content = node.Attributes["content"].Value;
					} else {
						p.content = node.InnerText;
					}
					if ( node.Attributes[ "datatype" ] != null ) {
						p.datatype = node.Attributes[ "datatype" ].Value;
					}
					
					if ( aboutTable.ContainsKey( subject ) ) {
						aboutTable[ subject ].Add( p );
					} else { 
						List<PredPair> tmp = new List<PredPair>();
						tmp.Add( p );
						aboutTable.Add( subject, tmp );
					}
									
					//Console.Out.WriteLine( aboutStack[ aboutStack.Count - 1] + " " + predicate + " \"" + text + "\"" );

				}
			}
			foreach ( XmlNode child in node.ChildNodes ) {
				Scan( child );	
			}			
			
			if ( node.Attributes != null ) {
				if ( node.Attributes["about"] != null ) {
					aboutStack.RemoveAt( aboutStack.Count-1 );		
				}
			}
		}		
		
		public XmlDocument toXML() {
			XmlDocument outdoc = new XmlDocument();	
			XmlNode xmlnode=outdoc.CreateNode(XmlNodeType.XmlDeclaration,"","");
			outdoc.AppendChild(xmlnode);
			//XmlSchema schema = new XmlSchema();
			//outdoc.Schemas.Add( schema );
			
			int ns_count = 1;
			foreach ( string subject in aboutTable.Keys ) {
				List<PredPair> pList = aboutTable[ subject ];				
				foreach ( PredPair p in pList ) { 
					int baseEnd = p.predicate.LastIndexOfAny( endChars );
					string ns = p.predicate.Substring(0, baseEnd+1);
					//string pred = p.predicate.Substring(baseEnd+1);
					if ( !nsTable.ContainsKey( ns ) ) {
						nsTable.Add(ns, "ns_" + ns_count);
						ns_count++;
					}
				}
			}
			
			XmlNode baseNode = outdoc.CreateElement("rdf", "RDF", rdfNS );
			//foreach ( string ns in nsTable.Keys ) {
			//	baseNode.Attributes.Append( outdoc.CreateAttribute( "xmlns:" + nsTable[ns], ns) );
			//}
			
			outdoc.AppendChild( baseNode );		
			
			foreach ( string subject in aboutTable.Keys ) {
				XmlElement curNode = outdoc.CreateElement("rdf", "Description", rdfNS );
				baseNode.AppendChild( curNode );
				
				curNode.SetAttribute("about", rdfNS, subject );				
				List<PredPair> pList = aboutTable[ subject ];				
				foreach ( PredPair p in pList ) { 
					if ( p.href != null ) {
						int baseEnd = p.predicate.LastIndexOfAny( endChars );
						string ns = p.predicate.Substring(0, baseEnd+1);
						string pred = p.predicate.Substring(baseEnd+1);				
						XmlElement predNode = outdoc.CreateElement( nsTable[ns], pred, ns); ;//( nsName, pred, ns );
						predNode.SetAttribute("resource", rdfNS, p.href );
						curNode.AppendChild( predNode );
					}
					if ( p.content != null ) {
						int baseEnd = p.predicate.LastIndexOfAny( endChars );
						string ns = p.predicate.Substring(0, baseEnd+1);
						string pred = p.predicate.Substring(baseEnd+1);				
						XmlElement predNode = outdoc.CreateElement( nsTable[ns], pred, ns); ;//( nsName, pred, ns );
						curNode.AppendChild( predNode );
						predNode.AppendChild( outdoc.CreateTextNode( p.content ) );
						if ( p.datatype != null ) {
							predNode.SetAttribute( "datatype", rdfNS, p.datatype );	
						}
					}
					//Console.Out.WriteLine( subject + " " + p.predicate );
				}
			}
			return outdoc;
		}
	}
}
