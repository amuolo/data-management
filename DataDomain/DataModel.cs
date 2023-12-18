﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataDomain;

public class DataModel
{
    public object Data { get; set; } = new object();

    public void Update(List<string> list)
    {
        Data = list;
    }
    
    public void Process()
    {
        // TODO
    }

    public List<string> GetPrintable()
    {
        return Data as List<string>?? new List<string>();
    }
}
