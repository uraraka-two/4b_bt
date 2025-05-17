using System;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ImprovedPentominoSolver
{

    // Nodeクラスの定義
    public class Node
    {
        public Node Left { get; set; }
        public Node Right { get; set; }
        public Node Up { get; set; }
        public Node Down { get; set; }
        public ColumnHeader Header { get; set; }
        public object NodeData { get; set; }

        public Node()
        {
            Left = Right = Up = Down = this;
        }
    }

    // ColumnHeaderクラスの定義
    public class ColumnHeader : Node
    {
        public int Count { get; set; }
        public bool Optional { get; set; }

        public ColumnHeader() : base()
        {
            Header = this;
        }

        public void Add(Node node)
        {
            node.Header = this;
            node.Down = this;
            node.Up = Up;
            Up.Down = node;
            Up = node;
            Count++;
        }
    }

    // FigureInfoレコードの定義
    public record FigureInfo(int FigureVariantId, int FigureId);

    // Positionレコードの定義
    public record Position(int Row, int Col);

    // RowInfoレコードの定義（FigureInfo と Position を統合）
    public record RowInfo(FigureInfo FigureInfo, Position Position)
    {
        // フィギュアがカバーするセル位置を取得するメソッド
        public IEnumerable<(int Row, int Col)> GetCoveredCells(Board board)
        {
            long[] figure = board.FigureSet.GetFigureByIndex(FigureInfo.FigureVariantId);
            for (int i = 0; i < figure.Length; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    if ((figure[i] & (1 << j)) != 0)
                    {
                        yield return (Position.Row + i, Position.Col + j);
                    }
                }
            }
        }
    }


    public class Board
    {
        public int[,] Field { get; set; }
        //public FigureSet FigureSet { get; set; }
        public FigureSet FigureSet { get; set; } = null!;

        public Board(int rows = 0, int cols = 0)
        {
            Field = new int[rows, cols];
            FigureSet = new FigureSet();
        }

        // フィールドにフィギュアを配置するか確認
        public bool CanPlaceFigure(long[] figure, int row, int col)
        {
            // フィギュアがフィールドを超えないかをチェック
            if (row + figure.Length > Field.GetLength(0)) return false;
            // フィギュアの各ビットをチェックして配置可能か確認
            return !figure.SelectMany((line, i) => Enumerable.Range(0, 5)
                    .Where(j => (line & (1 << j)) != 0)
                    .Select(j => (Row: row + i, Col: col + j)))
                .Any(pos => pos.Col >= Field.GetLength(1) || Field[pos.Row, pos.Col] != 1);
        }

        // フィギュアの配置に対応するcellIndexのリストを取得
        public List<int> GetCellIndexes(RowInfo rowInfo)
        {
            List<int> cellIndexes = new List<int>();
            foreach (var (r, c) in rowInfo.GetCoveredCells(this))
            {
                int index = GetCellIndex(r, c);
                cellIndexes.Add(index);
            }
            // フィギュアタイプ制約の列インデックスを追加
            cellIndexes.Add(GetTotalCells() + rowInfo.FigureInfo.FigureId);
            return cellIndexes;
        }

        // 2D座標をcellIndexに変換
        public int GetCellIndex(int row, int col)
        {
            if (row < 0 || row >= Field.GetLength(0) || col < 0 || col >= Field.GetLength(1))
            {
                throw new ArgumentException("Invalid cell position");
            }

            int index = 0;
            for (int i = 0; i < Field.GetLength(0); i++)
            {
                for (int j = 0; j < Field.GetLength(1); j++)
                {
                    if (Field[i, j] == 1)
                    {
                        if (i == row && j == col) return index;
                        index++;
                    }
                }
            }
            throw new ArgumentException("Invalid cell position");
        }

        // フィールド内の有効なセルの総数を取得
        public int GetTotalCells()
        {
            int count = 0;
            for (int i = 0; i < Field.GetLength(0); i++)
            {
                for (int j = 0; j < Field.GetLength(1); j++)
                {
                    if (Field[i, j] == 1) count++;
                }
            }
            return count;
        }
    }

    // DancingLinksクラスの定義
    public class DancingLinks
    {
        public ColumnHeader RootHeader { get; } = new ColumnHeader();
        public List<ColumnHeader> ColumnHeaders { get; } = new List<ColumnHeader>();
        public List<Node> Solution { get; } = new List<Node>();

        public DancingLinks(int numColumns)
        {
            for (int i = 0; i < numColumns; i++)
            {
                ColumnHeader header = new ColumnHeader();
                ColumnHeaders.Add(header);
                header.Left = RootHeader.Left;
                header.Right = RootHeader;
                RootHeader.Left.Right = header;
                RootHeader.Left = header;
            }
        }

        // AddRowメソッド
        public void AddRow(List<int> columns, RowInfo rowInfo)
        {
            List<Node> nodesInRow = new List<Node>();
            foreach (int col in columns)
            {
                Node newNode = new Node { NodeData = rowInfo };
                ColumnHeaders[col].Add(newNode);
                nodesInRow.Add(newNode);
            }
            // 行内のノードを相互にリンクする（左と右）
            for (int i = 0; i < nodesInRow.Count; i++)
            {
                nodesInRow[i].Left = nodesInRow[(i - 1 + nodesInRow.Count) % nodesInRow.Count];
                nodesInRow[i].Right = nodesInRow[(i + 1) % nodesInRow.Count];
            }
        }

        private void Cover(ColumnHeader col)
        {
            col.Right.Left = col.Left;
            col.Left.Right = col.Right;

            // 列に属する全ての行をカバー
            for (Node row = col.Down; row != col; row = row.Down)
            {
                for (Node j = row.Right; j != row; j = j.Right)
                {
                    j.Down.Up = j.Up;
                    j.Up.Down = j.Down;
                    j.Header.Count--;
                }
            }
        }

        private void Uncover(ColumnHeader col)
        {
            for (Node row = col.Up; row != col; row = row.Up)
            {
                for (Node j = row.Left; j != row; j = j.Left)
                {
                    j.Header.Count++;
                    j.Down.Up = j;
                    j.Up.Down = j;
                }
            }
            col.Right.Left = col;
            col.Left.Right = col;
        }

        public bool Solve()
        {
            if (RootHeader.Right == RootHeader)
            {
                return true; // 全ての列がカバーされた場合、解を見つけた
            }

            ColumnHeader col = GetMinColumn();
            if (col == null) return false;

            Cover(col);
            // 列に属する各行を試す
            for (Node row = col.Down; row != col; row = row.Down)
            {
                Solution.Add(row);
                // 行に含まれる全ての列をカバー
                for (Node j = row.Right; j != row; j = j.Right)
                {
                    Cover(j.Header);
                }

                if (Solve())
                {
                    return true;
                }

                Solution.RemoveAt(Solution.Count - 1);
                // 行に含まれる全ての列をアンカバー
                for (Node j = row.Left; j != row; j = j.Left)
                {
                    Uncover(j.Header);
                }
            }

            Uncover(col); // 列をアンカバー
            return false;
        }

        // 最小の列を取得するメソッド（ノード数が最小の列）
        private ColumnHeader GetMinColumn()
        {
            ColumnHeader minColumn = null;
            int minCount = int.MaxValue;

            // キャッシュの利用により列の再探索を回避（必要に応じて更新）
            for (ColumnHeader col = (ColumnHeader)RootHeader.Right; col != RootHeader; col = (ColumnHeader)col.Right)
            {
                if (!col.Optional && col.Count < minCount)
                {
                    minCount = col.Count;
                    minColumn = col;
                }
            }

            return minColumn;
        }
    }

    // Figureクラスの定義
    public class Figure
    {
        public int BaseFigureId { get; }
        public long[] FigureData { get; }
        public List<Figure> Variants { get; } = new List<Figure>();

        public Figure(int baseFigureId, long[] figureData)
        {
            BaseFigureId = baseFigureId;
            FigureData = figureData;
        }

        // バリアントを追加
        public void AddVariant(Figure variant)
        {
            Variants.Add(variant);
        }

        // フィギュアが同じかを比較
        public override bool Equals(object obj)
        {
            if (obj is Figure other)
            {
                if (FigureData.Length != other.FigureData.Length)
                    return false;

                for (int i = 0; i < FigureData.Length; i++)
                {
                    if (FigureData[i] != other.FigureData[i])
                        return false;
                }
                return true;
            }
            return false;
        }

        public override int GetHashCode()
        {
            int hash = BaseFigureId;
            foreach (var row in FigureData)
            {
                hash = hash * 31 + row.GetHashCode();
            }
            return hash;
        }
    }

    // FigureTransformerクラスの定義
    public static class FigureTransformer
    {
        const int MAX_DOTS = 5;

        // フィギュアを回転させるメソッド
        public static long[] Rotate(long[] figure)
        {
            long[] rotated = new long[MAX_DOTS];
            for (int row = 0; row < figure.Length; row++)
            {
                for (int col = 0; col < MAX_DOTS; col++)
                {
                    if ((figure[row] & (1 << col)) != 0)
                    {
                        int newCol = row;
                        int newRow = MAX_DOTS - 1 - col;
                        rotated[newRow] |= 1L << newCol;
                    }
                }
            }

            // 上方向へのシフト（不要な行の削除）
            return ShiftFigureUp(rotated);
        }

        // フィギュアを反転させるメソッド
        public static long[] Flip(long[] figure)
        {
            long[] flipped = new long[figure.Length];
            for (int row = 0; row < figure.Length; row++)
            {
                for (int col = 0; col < MAX_DOTS; col++)
                {
                    if ((figure[row] & (1 << col)) != 0)
                    {
                        int newCol = MAX_DOTS - 1 - col;
                        flipped[row] |= 1L << newCol;
                    }
                }
            }

            // 左方向へのシフト（不要な列の削除）
            return ShiftFigureLeft(flipped);
        }

        // フィギュアを上方向にシフト
        private static long[] ShiftFigureUp(long[] figure)
        {
            int firstNonZeroRow = 0;
            while (firstNonZeroRow < figure.Length && figure[firstNonZeroRow] == 0)
                firstNonZeroRow++;

            if (firstNonZeroRow == 0)
                return figure;

            long[] shifted = new long[figure.Length - firstNonZeroRow];
            Array.Copy(figure, firstNonZeroRow, shifted, 0, shifted.Length);
            return shifted;
        }

        // フィギュアを左方向にシフト
        private static long[] ShiftFigureLeft(long[] figure)
        {
            int minShift = MAX_DOTS;
            foreach (var row in figure)
            {
                if (row != 0)
                {
                    int firstSetBit = TrailingZeroCount(row);
                    if (firstSetBit < minShift)
                        minShift = firstSetBit;
                }
            }

            if (minShift == 0)
                return figure;

            long[] shifted = new long[figure.Length];
            for (int i = 0; i < figure.Length; i++)
            {
                shifted[i] = figure[i] >> minShift;
            }
            return shifted;
        }

        // TrailingZeroCountの代替実装
        private static int TrailingZeroCount(long value)
        {
            if (value == 0)
                return 64; // 64ビット全てが0の場合

            int count = 0;
            while ((value & 1) == 0)
            {
                count++;
                value >>= 1;
            }
            return count;
        }
    }

    // FigureFactoryクラスの定義
    public class FigureFactory
    {
        private readonly HashSet<char> _selected;

        private static byte[,] _activeSourceFiguresSet;
        public byte[,] ActiveSourceFiguresSet => _activeSourceFiguresSet;
        public int TypeCount => _activeSourceFiguresSet.GetLength(0);

        public List<Figure> Figures { get; } = new List<Figure>();

        public Dictionary<int, string> FigureIdToName { get; set; } = new Dictionary<int, string>();

        public FigureFactory(HashSet<char> selected)
        {
            _selected = selected;                   // 空集合なら「全部」
            InitializeFigures();
        }
        public FigureFactory() : this(new HashSet<char>()) {}

        private void InitializeFigures()
        {
            // ActiveSourceFiguresSetを初期化
            _activeSourceFiguresSet = GetSourceFiguresSetFromDat(out List<string> figureLetters);

            // フィギュアIDと名前の対応関係を構築
            this.FigureIdToName = CreateFigureIdToNameDictionary(figureLetters);

            for (int i = 0; i < _activeSourceFiguresSet.GetLength(0); i++)
            {
                long[] baseFigure = GetSourceFigure(i, _activeSourceFiguresSet);
                Figure baseFig = new Figure(i, baseFigure);
                Figures.Add(baseFig);

                // 生成されたバリアントをFigureに追加
                foreach (var variant in GenerateVariants(baseFig))
                {
                    baseFig.AddVariant(variant);
                    Figures.Add(variant);
                }
            }
        }

        // .d ファイルを選別して読み込む
        private byte[,] GetSourceFiguresSetFromDat(out List<string> figureLetters)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            // 埋め込まれたリソース名の一覧
            string[] resourceNames = asm.GetManifestResourceNames()
                                        .Where(n => n.Contains(".Data.") && n.EndsWith(".d", StringComparison.OrdinalIgnoreCase))
                                        .OrderBy(n => n)                // A~Z 順に並べ替え
                                        .ToArray();
        
            // selected によるフィルタ（空集合なら全部）
            if (_selected.Count > 0)
            {
                resourceNames = resourceNames
                    .Where(n =>
                    {
                        char letter = char.ToUpperInvariant(
                                        Path.GetFileNameWithoutExtension(n)[^1]); // 末尾1文字
                        return _selected.Contains(letter);
                    })
                    .ToArray();
            }
        
            figureLetters = resourceNames
                            .Select(n => Path.GetFileNameWithoutExtension(n)[^1].ToString())
                            .ToList();
        
            // バイト配列化
            var figArr = resourceNames.Select(n =>
            {
                using Stream s = asm.GetManifestResourceStream(n)!;
                using var ms   = new MemoryStream();
                s.CopyTo(ms);
                return ms.ToArray();
            }).ToArray();
        
            return To2D(figArr);
        }


        /// <summary>
        /// IDと名前の辞書を作成します。
        /// </summary>
        /// <param name="figureLetters">フィギュアの文字リスト</param>
        /// <returns>IDと名前の辞書</returns>
        static Dictionary<int, string> CreateFigureIdToNameDictionary(List<string> figureLetters)
        {
            Dictionary<int, string> figureIdToName = new Dictionary<int, string>();
            for (int i = 0; i < figureLetters.Count; i++)
            {
                figureIdToName.Add(i, figureLetters[i]);
            }
            return figureIdToName;
        }

        /// <summary>
        /// https://stackoverflow.com/a/26291720
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        static T[,] To2D<T>(T[][] source)
        {
            try
            {
                int FirstDim = source.Length;
                int SecondDim = source.GroupBy(row => row.Length).Single().Key; // throws InvalidOperationException if source is not rectangular

                var result = new T[FirstDim, SecondDim];
                for (int i = 0; i < FirstDim; ++i)
                    for (int j = 0; j < SecondDim; ++j)
                        result[i, j] = source[i][j];

                return result;
            }
            catch (InvalidOperationException)
            {
                throw new InvalidOperationException("The given jagged array is not rectangular.");
            }
        }

        // フィギュアの基礎データを取得
        private long[] GetSourceFigure(int index, byte[,] active)
        {
            long[] retArray = new long[5];
            int i = 0;
            for (; i < 5; i++)
            {
                if (active[index, i] == 0) break;
                retArray[i] = active[index, i];
            }
            if (i < 5)
            {
                Array.Resize(ref retArray, i);
            }
            return retArray;
        }

        // フィギュアの全バリアントを生成
        private IEnumerable<Figure> GenerateVariants(Figure baseFigure)
        {
            List<Figure> variants = new List<Figure>();
            long[] rotated = baseFigure.FigureData;

            // 4回の回転
            for (int r = 0; r < 4; r++)
            {
                rotated = FigureTransformer.Rotate(rotated);
                Figure rotatedFig = new Figure(baseFigure.BaseFigureId, rotated);
                if (!FigureExists(rotatedFig))
                {
                    variants.Add(rotatedFig);
                }

                // 反転
                long[] flipped = FigureTransformer.Flip(rotated);
                Figure flippedFig = new Figure(baseFigure.BaseFigureId, flipped);
                if (!FigureExists(flippedFig))
                {
                    variants.Add(flippedFig);
                }
            }

            return variants;
        }

        // フィギュアが既に存在するかチェック
        private bool FigureExists(Figure figure)
        {
            foreach (var fig in Figures)
            {
                if (fig.Equals(figure))
                    return true;
            }
            return false;
        }
    }

    // FigureSetクラスの定義
    public class FigureSet
    {
        private readonly FigureFactory factory;

        public FigureSet(HashSet<char> selected)
        {
            factory = new FigureFactory(selected);
        }
        public FigureSet()
        {
            factory = new FigureFactory();
        }

        public int TypeCount => factory.TypeCount;
        public FigureFactory Factory => factory;

        public long[] GetFigureByIndex(int figureIndex)
        {
            if (figureIndex < 0 || figureIndex >= factory.Figures.Count)
                throw new ArgumentOutOfRangeException(nameof(figureIndex));

            return (long[])factory.Figures[figureIndex].FigureData.Clone();
        }

        public int GetFigureIdByIndex(int figureIndex)
        {
            if (figureIndex < 0 || figureIndex >= factory.Figures.Count)
                throw new ArgumentOutOfRangeException(nameof(figureIndex));

            return factory.Figures[figureIndex].BaseFigureId;
        }

        public int Count => factory.Figures.Count;
    }

    // PentominoSolverクラスの定義
    public class PentominoSolver
    {
        private DancingLinks dancingLinks;
        private Board board;
        private FigureSet figureSet;
        private int FigureCellSize;
        private List<FigureInfo> figureInfoCache; // FigureInfoのキャッシュ

        public PentominoSolver(Board board)
        {
            this.board = board;
            this.figureSet = board.FigureSet;
            this.FigureCellSize = figureSet.TypeCount;
            dancingLinks = new DancingLinks(CountCells(board.Field) + figureSet.TypeCount);
            // FigureInfoのキャッシュを初期化
            figureInfoCache = new List<FigureInfo>();
            InitializeFigureInfoCache();
            InitializeMatrix();
        }
        // フィギュア情報をキャッシュに初期化する
        private void InitializeFigureInfoCache()
        {
            for (int figVariantId = 0; figVariantId < figureSet.Count; figVariantId++)
            {
                int figureId = figureSet.GetFigureIdByIndex(figVariantId);
                figureInfoCache.Add(new FigureInfo(figVariantId, figureId));
            }
        }

        // フィールド内のセル数をカウント
        private int CountCells(int[,] field)
        {
            int count = 0;
            for (int i = 0; i < board.Field.GetLength(0); i++)
            {
                for (int j = 0; j < board.Field.GetLength(1); j++)
                {
                    if (field[i, j] == 1) count++;
                }
            }
            return count;
        }

        private void InitializeMatrix()
        {
            List<(List<int>, RowInfo)> validPlacements = new();
            for (int figVariantId = 0; figVariantId < figureSet.Count; figVariantId++)
            {
                long[] figure = figureSet.GetFigureByIndex(figVariantId);
                FigureInfo figureInfo = figureInfoCache[figVariantId];

                var placements = Enumerable.Range(0, board.Field.GetLength(0))
                    .SelectMany(row => Enumerable.Range(0, board.Field.GetLength(1)), (row, col) => new { row, col })
                    .Where(pos => board.CanPlaceFigure(figure, pos.row, pos.col))
                    .Select(pos => new RowInfo(figureInfo, new Position(pos.row, pos.col)))
                    .Select(rowInfo => (AddFigureToConstraintMatrix(figure, rowInfo, board), rowInfo));

                validPlacements.AddRange(placements);
            }

/*
            // フィギュアIDと名前の対応関係を保持する辞書
            Dictionary<int, string> figureIdToName = figureSet.Factory.FigureIdToName;
            var q = from c in validPlacements
                    group c by c.Item2.FigureInfo.FigureId into g
                    select new { FigureId = figureIdToName[g.Key], Count = g.Count(), ColumnsToAdd = g.Select(x => x.Item1).ToList() };

            foreach (var item in q)
            {
                Console.WriteLine($"{item.Count}");
                foreach (var c in item.ColumnsToAdd)
                {
                    Console.WriteLine($"{item.FigureId}: {string.Join(',', c)}");
                }
            }
*/
        }

        private List<int> AddFigureToConstraintMatrix(long[] figure, RowInfo rowInfo, Board board)
        {
            List<int> validConstraintColumns = new List<int>();

            for (int i = 0; i < figure.Length; i++)
            {
                for (int j = 0; j < FigureCellSize; j++)
                {
                    if ((figure[i] & (1 << j)) != 0)
                    {
                        int cellIndex = board.GetCellIndex(rowInfo.Position.Row + i, rowInfo.Position.Col + j);
                        validConstraintColumns.Add(cellIndex);
                    }
                }
            }

            int totalFieldCells = board.GetTotalCells();

            // フィギュアの種類制約を追加
            int typeConstraintIndex = totalFieldCells + rowInfo.FigureInfo.FigureId;

            validConstraintColumns.Add(typeConstraintIndex);
            dancingLinks.AddRow(validConstraintColumns, rowInfo);
            return validConstraintColumns;
        }


        public bool Solve()
        {
            return dancingLinks.Solve();
        }

        public char[,] GetSolution()
        {
            char[,] solution = new char[board.Field.GetLength(0), board.Field.GetLength(1)];

            // 初期化
            for (int i = 0; i < solution.GetLength(0); i++)
            {
                for (int j = 0; j < solution.GetLength(1); j++)
                {
                    solution[i, j] = ' ';
                }
            }

            // ソリューションのノードを処理
            foreach (Node node in dancingLinks.Solution)
            {
                RowInfo data = (RowInfo)node.NodeData;
                foreach (var (r, c) in data.GetCoveredCells(board))
                {
                    // 境界チェックを追加
                    if (r >= 0 && r < board.Field.GetLength(0) &&
                        c >= 0 && c < board.Field.GetLength(1))
                    {
                        string figureName = figureSet.Factory.FigureIdToName[data.FigureInfo.FigureId];
                        solution[r, c] = figureName[0];
                    }
                    else
                    {
                        throw new IndexOutOfRangeException($"Figure placement out of bounds at row {r}, col {c}");
                    }
                }
            }

            return solution;
        }
    }

    // Programクラスの定義
    public class Program
    {
        static HashSet<char> ParsePieceFlags(string[] args)
        {
            // -f -v -p ... のように文字を渡されたら大文字にして格納
            var valid = new HashSet<char>{'F','V','P','U','N','Y',
                                          'L','T','W','X','Z','I'};   // 12種すべて列挙しておく
            var set   = new HashSet<char>();
        
            foreach (string a in args.Where(a => a.StartsWith('-')))
            {
                foreach (char c in a.Skip(1))          // -fvp → f,v,p のように分割
                {
                    char upper = char.ToUpperInvariant(c);
                    if (valid.Contains(upper))
                        set.Add(upper);
                }
            }
            return set;
        }

        static (int W,int H) DecideBoard(int pieceCount) => (5, pieceCount);

        static void Main(string[] args)
        {
            var selected = ParsePieceFlags(args); // 例: -v -p -u -n -y → {V,P,U,N,Y}
            int pieceCnt = selected.Count switch { 0 => 12, var n => n };
            //盤面を決定
            var (W,H) = DecideBoard(pieceCnt); // W=5, H=pieceCnt

            //Board を組み立て
            Board board = new Board();
            int[,] boardField = new int[H, W];         // ← 以前の (5,figureTypeCount) から変更
            for (int i = 0; i < H; i++)
                for (int j = 0; j < W; j++)
                    boardField[i, j] = 1;

            board.Field = boardField;

            FigureSet figureSet = new FigureSet(selected);
            board.FigureSet    = figureSet;
            PentominoSolver solver = new PentominoSolver(board);

            int figureTypeCount = pieceCnt;

            if (solver.Solve())
            {
                Console.WriteLine("Solution found!");
                char[,] solution = solver.GetSolution();
                PrintSolution(solution);
            }
            else
            {
                Console.WriteLine("No solution found.");
            }
            Console.WriteLine($"boardField is 5, {figureTypeCount}");
        }

        static void PrintSolution(char[,] solution)
        {
            for (int i = 0; i < solution.GetLength(0); i++)
            {
                for (int j = 0; j < solution.GetLength(1); j++)
                {
                    Console.Write(solution[i, j] + " ");
                }
                Console.WriteLine();
            }
            Console.ReadLine();
        }
    }
}
