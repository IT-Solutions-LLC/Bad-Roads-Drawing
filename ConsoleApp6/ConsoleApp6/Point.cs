using System;
using System.Collections.Generic;
using System.Text;

namespace ConsoleApp6
{
    class Point
    {
        public string Name { get; set; }
        public float Lat { get; set; }
        public float Lng { get; set; }

        public string Sensor { get; set; }

        public double Value { get; set; }

        override
        public string ToString()
        {
            return $"Name: {this.Name}, Location:{{{this.Lat}, {this.Lng}}}, Sensor type: {this.Sensor}, Value: {this.Value};";
        }
    }
}
