﻿using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KMG.Repository.Models;

public partial class OrderKoi
{
    public int OrderId { get; set; }

    public int KoiId { get; set; }

    public int? Quantity { get; set; }



    public virtual Koi? Koi { get; set; }



    public virtual Order? Order { get; set; }

    }


