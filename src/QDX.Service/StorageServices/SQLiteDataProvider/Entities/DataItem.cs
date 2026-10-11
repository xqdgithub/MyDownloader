using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace QDX.Serivices.StorageServices.SQLiteDataProvider.Entities;

[Table("data_item")]
public partial class DataItem
{

    public uint Id { get; set; }

    [Column("key")]
    public string Key { get; set; } = null!;

    [Column("value")]
    public string Value { get; set; } = null!;

    [Column("type")]
    public int Type { get; set; } = 0;
}
