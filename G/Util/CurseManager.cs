using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using OfficeOpenXml;
using MySql.Data.MySqlClient;
using Dapper;

#pragma warning disable CS1998

namespace G.Util
{
	class CurseNode
	{
		public bool IsCursed;
		public Dictionary<char, CurseNode> Dic = new Dictionary<char, CurseNode>();
	}

	[Refreshable]
	public class CurseManager
	{
		private static NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();

		private static readonly TimeSpan refreshTimeLimit = TimeSpan.FromSeconds(20);
		private static DateTime refreshedTime = DateTime.MinValue;

		private static CurseNode RootNode { get; set; } = new CurseNode();

		public static string FilePath { get; set; }
		public static int SheetIndex { get; set; }
		public static int Column { get; set; }
		public static int StartRow { get; set; }

		public static async Task RefreshAsync(bool enforced = false)
		{
			try
			{
				DateTime now = DateTime.Now;
				if (!enforced && (now - refreshedTime) < refreshTimeLimit) return;
				refreshedTime = now;

				if (string.IsNullOrWhiteSpace(FilePath)) return;
				if (FilePath.Contains(".xls") || FilePath.Contains(".xlsx"))
					Load(FilePath, SheetIndex, Column, StartRow);
				else
					Load(FilePath);
			}
			catch (Exception ex)
			{
				log.Error(ex.Message);
			}
		}

		public static bool Load(string filePath, int sheetIndex, int column, int startRow)
		{
			FilePath = filePath;
			SheetIndex = sheetIndex;
			Column = column;
			StartRow = startRow;

			var path = FileEx.SearchParentDirectory(filePath);
			if (path == null) return false;

			var list = new List<string>();

			using (var package = new ExcelPackage(new FileInfo(path)))
			using (var worksheet = package.Workbook.Worksheets[sheetIndex])
			{
				int rows = worksheet.Dimension.End.Row;
				int nullCount = 0;

				for (int row = startRow; row <= rows; row++)
				{
					var curseObj = worksheet.Cells[row, column].Value;
					if (curseObj == null)
					{
						nullCount++;
						if (nullCount > 10) break;
					}
					else
					{
						nullCount = 0;
						var curse = curseObj.ToString().Trim();
						if (curse.Length == 0) continue;
						list.Add(curse);
					}
				}
			}

			var rand = new Random();
			var rootNode = new CurseNode();

			foreach (var i in list.OrderBy(x => rand.Next()))
			{
				Add(rootNode, i);
			}

			RootNode = rootNode;

			return true;
		}

		public static bool Load(string filePath)
		{
			FilePath = filePath;

			var path = FileEx.SearchParentDirectory(filePath);
			if (path == null) return false;

			var rootNode = new CurseNode();
			var rand = new Random();

			var lines = File.ReadLines(path).OrderBy(x => rand.Next()); 
			foreach (var line in lines)
			{
				if (string.IsNullOrWhiteSpace(line)) continue;
				Add(rootNode, line.Trim());
			}

			RootNode = rootNode;

			return true;
		}

		private static bool Add(CurseNode rootNode, string text)
		{
			var currentNode = rootNode;
			text = text.Replace(" ", "").ToLower();

			foreach (var ch in text)
			{
				if (ch == ' ') continue;
				var beforeNode = currentNode;

				if (currentNode.Dic.TryGetValue(ch, out currentNode) == false)
				{
					currentNode = new CurseNode();
					beforeNode.Dic[ch] = currentNode;
				}
			}
			currentNode.IsCursed = true;

			return true;
		}

		public static bool Check(string text)
		{
			var textLower = text.ToLower();
			var result = _Search(textLower, 0);
			return result.IsCuresed;
		}

		public static (bool IsCursed, string Text) Refine(string text)
		{
			var isCursed = false;
			var chs = text.ToCharArray();
			var textLower = text.ToLower();

			for (int offset = 0; offset < chs.Length;)
			{
				var result = _Search(textLower, offset);

				if (result.IsCuresed)
				{
					isCursed = true;

					for (int i = 0; i < result.Skip; i++)
					{
						if (chs[offset + i] != ' ')
							chs[offset + i] = '*';
					}

					offset += result.Skip;
				}
				else
				{
					offset++;
				}
			}

			if (isCursed)
				return (true, new string(chs));
			else
				return (false, text);
		}

		private static (bool IsCuresed, int Skip) _Search(string text, int offset)
		{
			var node = RootNode;
			int skip = 0;

			for (int i = offset; i < text.Length; i++)
			{
				if (text[i] == ' ')
				{
					skip++;
				}
				else if (node.Dic.TryGetValue(text[i], out var nextNode))
				{
					skip++;
					node = nextNode;

					if (node.IsCursed)
					{
						return (true, skip);
					}
				}
				else
				{
					return (false, 1);
				}
			}

			return (false, 1);
		}
	}
}
