using Npgsql;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace ConsoleApp6
{

    struct test
    {
        public long Count;
        public long Value;
    }
    class Program
    {

        private static List<Point> Points = new List<Point>();
        private static List<Point> DangerousPoints = new List<Point>();
        private static NpgsqlConnection NpgsqlConnection = new NpgsqlConnection("Server=127.0.0.1;Port=5432;User Id=postgres;Password=cls;Database=BadRoads");
        public static int Count = 0;
        static void Main(string[] args)
        {
            NpgsqlConnection.Open();

            NpgsqlCommand command = new NpgsqlCommand("SELECT * FROM public.\"Points\" WHERE \"Value\" > 500 OR \"Value\" < -1000;", NpgsqlConnection);

            //command.CommandText = "SELECT * FROM public.\"Points\" WHERE \"Value\" > 500 OR \"Value\" < -1000;";
            var dataReader = command.ExecuteReader();
            while (dataReader.Read())
            {
                var point = new Point()
                {
                    Lat = (float)(double)dataReader["Lat"],
                    Lng = (float)(double)dataReader["Lng"],
                    Value = (double)dataReader["Value"]
                };
                DangerousPoints.Add(point);
            }
        }

        //Lat - Y Lng - X
        public static void DrawCells()
        {
            List<Cell> AllCells = new List<Cell>();
            for (int zoom = 0; zoom < 19; zoom++)
            {
                //Width of Azerbaijan (metr)
                int CountryWidth = 510428;
                //Height of Azerbaijan (metr)
                int CountryHeight = 413088;
                //width of the cell. It depends on zoom
                int SquareWidth = 10 * (int)Math.Pow(19 - zoom, 1.5);
                //Creating two dimensional array
                long[][] cells = new long[(int)(CountryHeight / SquareWidth)][];
                long MaxCount = 0;
                //Lat of the left bottom corner of Azerbaijan
                var LatMin = 38.3567089966;
                //Lng of the left bottom corner of Azerbaijan
                var LngMin = 44.5243410194;
                //step of the each cell
                var LatStep = ((42.0716996794 - 38.3567089966) * SquareWidth) / CountryHeight;
                //step of the each cell
                var LngStep = ((50.7096437529 - 44.5243410194) * SquareWidth) / CountryWidth;

                //Creating two dimensional array
                for (int i = 0; i < cells.Length; i++)
                    cells[i] = new long[(int)(CountryWidth / SquareWidth)];

                //Grouping dangerouspoints in the cells
                for (int i = 0; i < DangerousPoints.Count; i++)
                {
                    //Find cell by point coords
                    int LatIndex = (int)MathF.Floor((float)((DangerousPoints[i].Lat - LatMin) / LatStep));
                    //Find cell by point coords
                    int LngIndex = (int)MathF.Floor((float)((DangerousPoints[i].Lng - LngMin) / LngStep));

                    //check cell coords 
                    if (LatIndex < (int)(CountryHeight / SquareWidth) && LngIndex < (int)(CountryWidth / SquareWidth))
                    {
                        cells[LatIndex][LngIndex]++;
                        if (cells[LatIndex][LngIndex] > MaxCount)
                            MaxCount = cells[LatIndex][LngIndex];
                    }
                }

                //Transfer all cells from two dimensional array to list where amount of dangerous point more than 0
                for (int i = 0; i < cells.Length; i++)
                {
                    for (int j = 0; j < cells[i].Length; j++)
                    {
                        if (cells[i][j] != 0)
                        {
                            var t = new Cell()
                            {
                                Count = cells[i][j],
                                X = j,
                                Y = i,
                                Lat = (float)(LatMin + LatStep * i),
                                Lng = (float)(LngMin + LngStep * j)
                            };
                            AllCells.Add(t);
                        }
                    }
                }

                List<PointF> tile = new List<PointF>();
                var image = new Bitmap(256, 256);

                //Creating Graphics object which draw in image(png)
                using (var g = Graphics.FromImage(image))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                    for (int i = 0; i < AllCells.Count; i++)
                    {
                        //Convert point Lat long to Tile(x,y)
                        var tileX = long2tile(AllCells[i].Lng, zoom);
                        var tileY = lat2tile(AllCells[i].Lat, zoom);

                        //check if tile exists
                        if (!File.Exists($"\\Cells\\{tileX}A{tileY}A{zoom}.png"))
                        {
                            //add coords(lat,lng) left bottom of tile
                            tile.Add(new PointF((float)tile2long(tileX, zoom), (float)tile2lat(tileY, zoom)));
                            int tile2Lng = (int)MathF.Floor((tileX * 256 + 256) / 256);
                            int tile2Lat = (int)MathF.Floor((tileY * 256 + 256) / 256);
                            //add coords(lat,lng) right top of tile
                            tile.Add(new PointF((float)tile2long(tile2Lng, zoom), (float)tile2lat(tile2Lat, zoom)));
                            //find difference between right top corner and left bottom corner 
                            var difX = tile[1].X - tile[0].X;
                            var difY = tile[1].Y - tile[0].Y;
                            //creating list which keep dangerous cells of current tile 
                            List<Rectangle> points = new List<Rectangle>();

                            //pass over list of cells(10metrX10metr)
                            for (int j = 0; j < AllCells.Count; j++)
                            {
                                //check if cell(10mX10m) located in the current tile(256px 256px)
                                if (AllCells[j].Lat <= tile[0].Y + LatStep && AllCells[j].Lat >= tile[1].Y - LatStep
                                    && AllCells[j].Lng >= tile[0].X + LngStep && AllCells[j].Lng <= tile[1].X - LngStep)
                                {
                                    //add cell into list of dangerous cells 
                                    //(Cell object consists of
                                    //X - coord in png image 
                                    //Y - coord in png image
                                    //Lat - map coord of cell
                                    //Lng - map coord of cell
                                    //Width - width of cell (by default 10m X 10m) in the image
                                    //Color - color of the cell)

                                    //add cell to list                                    
                                    points.Add(new Rectangle()
                                    {
                                        X = (float)((AllCells[j].Lng - tile[0].X) * 256 / difX),
                                        Y = (float)((AllCells[j].Lat - tile[0].Y) * 256 / difY),
                                        Lat = AllCells[j].Lat,
                                        Lng = AllCells[j].Lng,
                                        Width = (float)((AllCells[j].Lng + LngStep - tile[0].X) * 256 / difX) - (float)((AllCells[j].Lng - tile[0].X) * 256 / difX),
                                        Color = AllCells[j].Count > 0 && AllCells[j].Count <= 3 * (19 - zoom) ? Color.Green : AllCells[j].Count > 3 * (19 - zoom) && AllCells[j].Count <= 15 * (19 - zoom) ? Color.Yellow : Color.Red
                                    });
                                }
                            }
                            //pass over list of the dangerous cells and draw them
                            for (int j = 0; j < points.Count; j++)
                                g.FillEllipse(new SolidBrush(points[j].Color), points[j].X, points[j].Y, points[j].Width, points[j].Width);
                            //save image as png with specific name 
                            image.Save($"\\Cells\\{tileX}A{tileY}A{zoom}.png");
                            //clear list
                            tile.Clear();
                            //clear graphics canvas
                            g.Clear(Color.White);
                            //make canvas transparent
                            g.Clear(Color.Transparent);
                        }
                    }
                }
                //clear list
                AllCells.Clear();
            }
        }


        public static void DrawLine()
        {
            for (int zoom = 0; zoom < 20; zoom++)
            {
                List<PointF> tile = new List<PointF>();
                var image = new Bitmap(256, 256);
                using (var g = Graphics.FromImage(image))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.ScaleTransform(1.0f, -1.0f);
                    g.TranslateTransform(0, -256);

                    for (int i = 0; i < DangerousPoints.Count; i++)
                    {
                        var x = long2tile(DangerousPoints[i].Lng, zoom);
                        var y = lat2tile(DangerousPoints[i].Lat, zoom);
                        if (!File.Exists($"\\PNG\\{x}A{y}A{zoom}.png"))
                        {
                            tile.Add(new PointF((float)tile2long(x, zoom), (float)tile2lat(y, zoom)));
                            int temp1 = (int)MathF.Floor((x * 256 + 256) / 256);
                            int temp2 = (int)MathF.Floor((y * 256 + 256) / 256);
                            tile.Add(new PointF((float)tile2long(temp1, zoom), (float)tile2lat(temp2, zoom)));
                            var difX = tile[1].X - tile[0].X;
                            var difY = tile[1].Y - tile[0].Y;

                            List<PointF> points = new List<PointF>();

                            for (int j = 0; j < DangerousPoints.Count; j++)
                            {
                                if (DangerousPoints[j].Lat <= tile[0].Y && DangerousPoints[j].Lat >= tile[1].Y
                                    && DangerousPoints[j].Lng >= tile[0].X && DangerousPoints[j].Lng <= tile[1].X)
                                {
                                    points.Add(new PointF((float)((DangerousPoints[j].Lng - tile[0].X) * 256 / difX), (float)((DangerousPoints[j].Lat - tile[0].Y) * 256 / difY)));
                                }
                            }

                            for (int j = 0; j < points.Count; j++)
                            {
                                g.FillEllipse(new SolidBrush(Color.Red), new RectangleF(points[j].X - 6f, points[j].Y - 6f, 12f, 12f));
                            }
                            image.Save($"\\PNG\\{x}A{y}A{zoom}.png");
                            tile.Clear();
                            g.Clear(Color.White);
                            g.Clear(Color.Transparent);
                        }
                    }
                    g.Clear(Color.White);
                    g.Clear(Color.Transparent);
                }
                Console.WriteLine("Finished");
            }

        }



        public static double tile2long(int x, int z)
        {
            return (x / Math.Pow(2, z) * 360 - 180);
        }

        public static double tile2lat(int y, int z)
        {
            var n = Math.PI - 2 * Math.PI * y / Math.Pow(2, z);
            return (180 / Math.PI * Math.Atan(0.5 * (Math.Exp(n) - Math.Exp(-n))));
        }

        public static int long2tile(float lon, int z)
        {
            return (int)(Math.Floor((lon + 180) / 360 * Math.Pow(2, z)));
        }
        public static int lat2tile(float lat, int z)
        {
            return (int)(Math.Floor((1 - Math.Log(Math.Tan(lat * Math.PI / 180) + 1 / Math.Cos(lat * Math.PI / 180)) / Math.PI) / 2 * Math.Pow(2, z)));
        }



        //public static void DrawLine(List<PointD> line, List<PointD> tile)
        //{
        //    var image = new Bitmap(256, 256);
        //    using (var g = Graphics.FromImage(image))
        //    {
        //        //g.ScaleTransform(1.0f, -1.0f);
        //        //g.TranslateTransform(0, -256);
        //        g.SmoothingMode = SmoothingMode.AntiAlias;
        //        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        //        var difX = tile[1].X - tile[0].X;
        //        var difY = tile[1].Y - tile[0].Y;
        //        PointF[] points = new PointF[line.Count];
        //        //g.FillRectangle(new SolidBrush(Color.Black), 0, 0, 256, 256);
        //        for (int i = 0; i < line.Count; i++)
        //        {
        //            points[i] = new PointF((float)((line[i].X - tile[0].X) * 256 / difX), (float)((line[i].Y - tile[0].Y) * 256 / difY));
        //        }
        //        g.DrawLines(new Pen(Color.Green, 5), points);
        //        for (int i = 0; i < points.Length; i++)
        //        {
        //            if (i == 1)
        //                g.FillEllipse(new SolidBrush(Color.Yellow), new RectangleF(points[i].X - 6f, points[i].Y - 6f, 12f, 12f));
        //            else if (i == 2)
        //                g.FillEllipse(new SolidBrush(Color.Green), new RectangleF(points[i].X - 6f, points[i].Y - 6f, 12f, 12f));
        //            else
        //                g.FillEllipse(new SolidBrush(Color.Red), new RectangleF(points[i].X - 6f, points[i].Y - 6f, 12f, 12f));
        //        }
        //    }
        //    image.Save("C:\\Users\\AYDIN CEFEROV\\Downloads\\9ea2d2092f73b3afdd0422df02f57208332.png");

        //}




        //                    for (int i = 0; i<DangerousPoints.Count; i++)
        //            {
        //                int LatIndex = (int)MathF.Floor((float)((DangerousPoints[i].Lat - LatMin) / Ystep));
        //        int LngIndex = (int)MathF.Floor((float)((DangerousPoints[i].Lng - LngMin) / Xstep));
        //                if (LatIndex< 41308 && LngIndex< 51042)
        //                {
        //                    var cell = Cells.FirstOrDefault((obj) => obj.X == LngIndex && obj.Y == LatIndex);
        //                    if (cell == null)
        //                    {
        //                        cell = new Cell()
        //        {
        //            X = LngIndex,
        //                            Y = LatIndex
        //                        };
        //        Cells.Add(cell);
        //                    }
        //    cell.Count++;
        //                    if (MaxCount<cell.Count)
        //                        MaxCount = cell.Count;
        //                }
        //Console.WriteLine(i + "  " + DangerousPoints.Count);
        //            }
        //            Console.WriteLine(Cells.Count);

    }
}




//Db and xlsx

//System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
//customCulture.NumberFormat.NumberDecimalSeparator = ".";

//System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;
//NpgsqlConnection.Open();
//System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

//FileStream stream = File.Open("C:\\Users\\AYDIN CEFEROV\\Downloads\\file3.xlsx", FileMode.Open, FileAccess.Read);
//IExcelDataReader reader;

//reader = ExcelDataReader.ExcelReaderFactory.CreateReader(stream);

//var conf = new ExcelDataSetConfiguration
//{
//    ConfigureDataTable = _ => new ExcelDataTableConfiguration
//    {
//        UseHeaderRow = true
//    }
//};

//var dataSet = reader.AsDataSet(conf);
//var dataTable = dataSet.Tables[0];

//for (int i = 0; i < dataTable.Rows.Count; i++)
//{
//    if (!((string)dataTable.Rows[i][2]).Contains("not") && (double)dataTable.Rows[i][4] != 0)
//    {
//        string[] positions = ((string)dataTable.Rows[i][2]).Split(",");
//        //var point = new Point()
//        //{
//        var Name = (string)dataTable.Rows[i][0];
//        var Sensor = (string)dataTable.Rows[i][3];
//        var Value = (double)dataTable.Rows[i][4];
//        if (positions.Length > 1)
//        {
//            var Lat = float.Parse(positions[0], CultureInfo.InvariantCulture.NumberFormat);
//            var Lng = float.Parse(positions[1], CultureInfo.InvariantCulture.NumberFormat);

//            NpgsqlCommand cmd = NpgsqlConnection.CreateCommand();
//            cmd.CommandText = $"INSERT INTO public.\"Points\" values('{Name}', {Lat}, {Lng}, '{Sensor}', {Value})";
//            cmd.ExecuteNonQuery();
//        }
//        //};
//        //Points.Add(point);
//    }
//}





//public static void DrawCells()
//{
//    Console.WriteLine("Began");
//    List<Cell> test = new List<Cell>();
//    long[][] cells = new long[41308][];
//    long MaxCount = 0;
//    var LatMin = 38.3567089966;
//    var LngMin = 44.5243410194;
//    var Ystep = ((42.0716996794 - 38.3567089966) * 10) / 413088;
//    var Xstep = ((50.7096437529 - 44.5243410194) * 10) / 510428;


//    for (int i = 0; i < cells.Length; i++)
//    {
//        cells[i] = new long[51042];
//    }
//    for (int i = 0; i < DangerousPoints.Count; i++)
//    {
//        int LatIndex = (int)MathF.Floor((float)((DangerousPoints[i].Lat - LatMin) / Ystep));
//        int LngIndex = (int)MathF.Floor((float)((DangerousPoints[i].Lng - LngMin) / Xstep));
//        if (LatIndex < 41308 && LngIndex < 51042)
//        {
//            cells[LatIndex][LngIndex]++;
//            if (cells[LatIndex][LngIndex] > MaxCount)
//                MaxCount = cells[LatIndex][LngIndex];
//        }
//    }
//    Console.WriteLine("cells");
//    for (int i = 0; i < 41308; i++)
//    {
//        for (int j = 0; j < 51042; j++)
//        {
//            if (cells[i][j] != 0)
//            {
//                Count++;
//                var t = new Cell()
//                {
//                    Count = cells[i][j],
//                    X = j,
//                    Y = i,
//                    Lat = (float)(LatMin + Ystep * i),
//                    Lng = (float)(LngMin + Xstep * j)
//                };
//                test.Add(t);
//            }
//        }
//    }
//    for (int zoom = 0; zoom < 19; zoom++)
//    {
//        List<PointF> tile = new List<PointF>();
//        var image = new Bitmap(256, 256);
//        using (var g = Graphics.FromImage(image))
//        {
//            g.SmoothingMode = SmoothingMode.AntiAlias;
//            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
//            for (int i = 0; i < test.Count; i++)
//            {
//                var x = long2tile(test[i].Lng, zoom);
//                var y = lat2tile(test[i].Lat, zoom);
//                if (!File.Exists($"C:\\Users\\AYDIN CEFEROV\\Downloads\\Cells\\{x}A{y}A{zoom}.png"))
//                {
//                    tile.Add(new PointF((float)tile2long(x, zoom), (float)tile2lat(y, zoom)));
//                    int temp1 = (int)MathF.Floor((x * 256 + 256) / 256);
//                    int temp2 = (int)MathF.Floor((y * 256 + 256) / 256);
//                    tile.Add(new PointF((float)tile2long(temp1, zoom), (float)tile2lat(temp2, zoom)));
//                    var difX = tile[1].X - tile[0].X;
//                    var difY = tile[1].Y - tile[0].Y;
//                    List<Rectangle> points = new List<Rectangle>();
//                    for (int j = 0; j < test.Count; j++)
//                    {
//                        if (test[j].Lat + Ystep <= tile[0].Y && test[j].Lat >= tile[1].Y
//                            && test[j].Lng + Xstep >= tile[0].X && test[j].Lng <= tile[1].X)
//                        {
//                            points.Add(new Rectangle()
//                            {
//                                X = (float)((test[j].Lng - tile[0].X) * 256 / difX),
//                                Y = (float)((test[j].Lat - tile[0].Y) * 256 / difY),
//                                Width = (float)((test[j].Lng + Xstep - tile[0].X) * 256 / difX) - (float)((test[j].Lng - tile[0].X) * 256 / difX),
//                                Color = test[j].Count > 0 && test[j].Count <= 3 ? Color.Green : test[j].Count > 3 && test[j].Count <= 15 ? Color.Yellow : Color.Red
//                            });
//                        }
//                    }
//                    Console.WriteLine(i);
//                    for (int j = 0; j < points.Count; j++)
//                    {
//                        g.FillEllipse(new SolidBrush(points[j].Color), points[j].X, points[j].Y, points[j].Width, points[j].Width);
//                        //g.DrawRectangle(new Pen(Color.Black), points[j].X, points[j].Y, points[j].Width, points[j].Width);
//                    }
//                    image.Save($"C:\\Users\\AYDIN CEFEROV\\Downloads\\Cells\\{x}A{y}A{zoom}.png");
//                    tile.Clear();
//                    g.Clear(Color.White);
//                    g.Clear(Color.Transparent);
//                }
//            }
//        }
//    }
//}





//public static void DrawCells()
//{
//    Console.WriteLine("Began");
//    List<Cell> test = new List<Cell>();
//    for (int zoom = 0; zoom < 19; zoom++)
//    {
//        int CountryWidth = 510428;
//        int CountryHeight = 413088;
//        int SquareWidth = 10 * (int)Math.Pow(19 - zoom, 1.5);
//        Console.WriteLine(SquareWidth);
//        long[][] cells = new long[(int)(CountryHeight / SquareWidth)][];
//        long MaxCount = 0;
//        var LatMin = 38.3567089966;
//        var LngMin = 44.5243410194;
//        var Ystep = ((42.0716996794 - 38.3567089966) * SquareWidth) / CountryHeight;
//        var Xstep = ((50.7096437529 - 44.5243410194) * SquareWidth) / CountryWidth;


//        for (int i = 0; i < cells.Length; i++)
//        {
//            cells[i] = new long[(int)(CountryWidth / SquareWidth)];
//        }
//        for (int i = 0; i < DangerousPoints.Count; i++)
//        {
//            int LatIndex = (int)MathF.Floor((float)((DangerousPoints[i].Lat - LatMin) / Ystep));
//            int LngIndex = (int)MathF.Floor((float)((DangerousPoints[i].Lng - LngMin) / Xstep));
//            if (LatIndex < (int)(CountryHeight / SquareWidth) && LngIndex < (int)(CountryWidth / SquareWidth))
//            {
//                cells[LatIndex][LngIndex]++;
//                if (cells[LatIndex][LngIndex] > MaxCount)
//                    MaxCount = cells[LatIndex][LngIndex];
//            }
//        }
//        Console.WriteLine("cells");
//        for (int i = 0; i < cells.Length; i++)
//        {
//            for (int j = 0; j < cells[i].Length; j++)
//            {
//                if (cells[i][j] != 0)
//                {
//                    Count++;
//                    var t = new Cell()
//                    {
//                        Count = cells[i][j],
//                        X = j,
//                        Y = i,
//                        Lat = (float)(LatMin + Ystep * i),
//                        Lng = (float)(LngMin + Xstep * j)
//                    };
//                    test.Add(t);
//                }
//            }
//        }

//        List<PointF> tile = new List<PointF>();
//        var image = new Bitmap(256, 256);
//        using (var g = Graphics.FromImage(image))
//        {
//            g.SmoothingMode = SmoothingMode.AntiAlias;
//            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
//            for (int i = 0; i < test.Count; i++)
//            {
//                var x = long2tile(test[i].Lng, zoom);
//                var y = lat2tile(test[i].Lat, zoom);
//                if (!File.Exists($"C:\\Users\\AYDIN CEFEROV\\Downloads\\Cells\\{x}A{y}A{zoom}.png"))
//                {
//                    tile.Add(new PointF((float)tile2long(x, zoom), (float)tile2lat(y, zoom)));
//                    int temp1 = (int)MathF.Floor((x * 256 + 256) / 256);
//                    int temp2 = (int)MathF.Floor((y * 256 + 256) / 256);
//                    tile.Add(new PointF((float)tile2long(temp1, zoom), (float)tile2lat(temp2, zoom)));
//                    var difX = tile[1].X - tile[0].X;
//                    var difY = tile[1].Y - tile[0].Y;
//                    List<Rectangle> points = new List<Rectangle>();
//                    for (int j = 0; j < test.Count; j++)
//                    {
//                        if (test[j].Lat + Ystep <= tile[0].Y && test[j].Lat >= tile[1].Y
//                            && test[j].Lng + Xstep >= tile[0].X && test[j].Lng <= tile[1].X)
//                        {
//                            points.Add(new Rectangle()
//                            {
//                                X = (float)((test[j].Lng - tile[0].X) * 256 / difX),
//                                Y = (float)((test[j].Lat - tile[0].Y) * 256 / difY),
//                                Lat = test[j].Lat,
//                                Lng = test[j].Lng,
//                                Width = (float)((test[j].Lng + Xstep - tile[0].X) * 256 / difX) - (float)((test[j].Lng - tile[0].X) * 256 / difX),
//                                Color = test[j].Count > 0 && test[j].Count <= 3 * (19 - zoom) ? Color.Green : test[j].Count > 3 * (19 - zoom) && test[j].Count <= 15 * (19 - zoom) ? Color.Yellow : Color.Red
//                            });
//                        }
//                    }
//                    for (int j = 0; j < points.Count; j++)
//                    {
//                        g.FillEllipse(new SolidBrush(points[j].Color), points[j].X, points[j].Y, points[j].Width, points[j].Width);
//                        //g.DrawString($"Lat: {points[j].Lat}, Lng: {points[j].Lng};", new Font(new FontFamily(System.Drawing.Text.GenericFontFamilies.Monospace), 15), new SolidBrush(Color.Black), new PointF(points[j].X, points[j].Y));
//                        //g.DrawRectangle(new Pen(Color.Black), points[j].X, points[j].Y, points[j].Width, points[j].Width);
//                    }
//                    image.Save($"C:\\Users\\AYDIN CEFEROV\\Downloads\\Cells\\{x}A{y}A{zoom}.png");
//                    tile.Clear();
//                    g.Clear(Color.White);
//                    g.Clear(Color.Transparent);
//                }
//            }
//        }
//        test.Clear();
//    }
//}
