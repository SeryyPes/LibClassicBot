﻿//Static methods to perform conversions on ints, strings, shorts, and bytes
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
namespace LibClassicBot
{
	/// <summary>
	/// Various utilities relating to chat messages and logging in.
	/// </summary>
	public static class Extensions
	{	
		/// <summary>
		/// Strips all colours from a given chatline. Based on code from fCraft.
		/// </summary>
		public static string StripColors( string input ) {
			if( input.IndexOf( '&' ) == -1 ) {//No & symbols were found.
				return input;
			} else {
				StringBuilder output = new StringBuilder( input.Length );
				for( int i = 0; i < input.Length; i++ ) {
					if( input[i] == '&' ) {
						if( i == input.Length - 1 ) {
							break;
						}
						i++;
					} else {
						output.Append( input[i] );
					}
				}
				return output.ToString();
			}
		}

		/// <summary>
		/// Converts a string into a byte array compatible with classic servers.
		/// </summary>
		/// <param name="s">The string used to convert.</param>
		/// <returns>A converted byte array of the string.</returns>
		public static byte[] StringToBytes(string message)
		{
			if(message.Length > 64) message = message.Substring(0, 64); //Failsafe
			byte[] MessageBytes = System.Text.Encoding.ASCII.GetBytes(message);
			byte[] FullArray = new byte[64];
			for (int i = 0; i < FullArray.Length; i++)
			{
				FullArray[i] = 32; //' ' Vanilla doesn't have a problem if we use 0, but other servers do.
			}
			Buffer.BlockCopy(MessageBytes,0,FullArray,0,MessageBytes.Length);
			return FullArray;
		}

		#region Logging In
		public static void Login(string username, string password, string gameurl, out IPAddress _serverIP, out int _serverPort, out string verificationkey)
		{
			string html = LoginAndReadPage(username, password, gameurl);
			string serveraddress = ReadValue(html.Substring(html.IndexOf("\"server\""), 40));
			string port = ReadValue(html.Substring(html.IndexOf("\"port\""), 40));
			string mppass = ReadValue(html.Substring(html.IndexOf("\"mppass\""), 80));
			verificationkey = mppass;
			_serverIP = IPAddress.Parse(serveraddress);
			_serverPort = Convert.ToInt16(port);
		}
		private static string LoginAndReadPage(string username, string password, string gameurl)
		{
			//Check if we have an invalid URL first.
			LoginCheck(username, password);
			//Step 1.
			LoginCookie(username,password);
			
			//Step 2.
			//Go to game url and GET using JSESSIONID cookie and _uid cookie.
			//Parse the page to find server, port, mpass strings.
			WebRequest step3Request = HttpWebRequest.Create(gameurl);
			foreach (string cookie in loggedincookie)
			{
				step3Request.Headers.Add(cookie);
			}
			using (Stream s4 = step3Request.GetResponse().GetResponseStream())
			{
				string html = new StreamReader(s4).ReadToEnd();
				return html;
				
			}
		}
		private static string ReadValue(string s)
		{
			string start = "value=\"";
			string end = "\"";
			string ss = s.Substring(s.IndexOf(start) + start.Length);
			ss = ss.Substring(0, ss.IndexOf(end));
			return ss;
		}
		
		static List<string> loggedincookie = new List<string>();
		
		static void LoginCheck(string username, string password)
		{
			string loginString = String.Format( "username={0}&password={1}", username, password);
			string loginResponse = MakeLoginRequest( loginString );
			if( loginResponse.Contains( "Oops, unknown username or password." ) ) { }
			//First, as occasionly we still have the username in the page
			else if ( loginResponse.Contains( username ) )
				return;
			throw new InvalidOperationException();
		}
		
		static string MakeLoginRequest( string dataToPost )
		{
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create( "https://minecraft.net/login" );
			request.CookieContainer = new CookieContainer();
			request.Method = "POST";
			request.ContentType = "application/x-www-form-urlencoded";
			byte[] data = Encoding.UTF8.GetBytes( dataToPost );
			request.ContentLength = data.Length;
			request.GetRequestStream().Write( data, 0, data.Length );
			HttpWebResponse response = (HttpWebResponse)request.GetResponse();
			using( StreamReader reader = new StreamReader(response.GetResponseStream() ))
			{ return reader.ReadToEnd(); }
		}
		
		static void LoginCookie(string username, string password)
		{
			
			string formData = string.Format("username={0}&password={1}", username, password);
			//Step 1.
			//Go to http://minecraft.net/login and POST "username={0}&password={1}" using JSESSIONID cookie.
			//You will receive logged in cookie ("_uid").
			//Because of multipart http page, HttpWebRequest has some trouble receiving cookies in step 2,
			//so it is easier to just use raw TcpClient for this.
			{
				using (TcpClient step2Client = new TcpClient("minecraft.net", 80))
				{
					NetworkStream stream = step2Client.GetStream();
					StreamWriter sw = new StreamWriter(stream);

					sw.WriteLine("POST /login HTTP/1.0");
					sw.WriteLine("Content-Type: application/x-www-form-urlencoded");
					sw.WriteLine("Content-Length: " + formData.Length);
					sw.WriteLine("");
					sw.WriteLine(formData);
					sw.Flush();
					
					StreamReader sr = new StreamReader(stream);
					loggedincookie.Clear(); //Clear all existing cookies.
					for (; ; )
					{
						string s = sr.ReadLine();
						if (s == null)
						{
							break;
						}
						if (s.Contains("Set-Cookie"))
						{
							loggedincookie.Add(s);
						}
					}
					sr.Dispose();
					sw.Dispose();
				}
			}
			
			for (int i = 0; i < loggedincookie.Count; i++)
			{
				loggedincookie[i] = loggedincookie[i].Replace("Set-", "");
			}
		}
		#endregion
	}
}