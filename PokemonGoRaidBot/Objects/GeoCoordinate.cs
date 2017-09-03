using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonGoRaidBot.Objects
{
    public class GeoCoordinate : IEquatable<GeoCoordinate>
    {
        //private readonly double? latitude;
        //private readonly double? longitude;

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public bool HasValue { get { return Latitude.HasValue && Longitude.HasValue; } }

        public GeoCoordinate () { }

        public GeoCoordinate(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
        }

        public override string ToString()
        {
            return string.Format("{0},{1}", Latitude, Longitude);
        }
        public override bool Equals(Object other)
        {
            return other is GeoCoordinate && Equals((GeoCoordinate)other);
        }

        public bool Equals(GeoCoordinate other)
        {
            return Latitude == other.Latitude && Longitude == other.Longitude;
        }

        public override int GetHashCode()
        {
            return Latitude.GetHashCode() ^ Longitude.GetHashCode();
        }
    }
}
