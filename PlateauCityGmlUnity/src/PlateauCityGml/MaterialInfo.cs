using System;
using System.Collections.Generic;
using System.Linq;
//using System.Numerics;
using UnityEngine;
using System.Text;
using System.Threading.Tasks;

namespace PlateauCityGml
{
    public class MaterialInfo
    {
        public List<string> Files;
        public Dictionary<string, (int index, Color[] color)> Map { get; set; }
    }
}
