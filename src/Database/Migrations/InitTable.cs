using FluentMigrator;

namespace SLAYER_Duel.Database.Migrations;

[Migration(2026020600, "Initialize SLAYER_Duel table")]
public class InitTable : Migration
{
    public override void Up()
    {
        if (!Schema.Table("SLAYER_Duel").Exists())
        {
            Create.Table("SLAYER_Duel")
                .WithColumn("steamid").AsInt64().PrimaryKey().NotNullable()
                .WithColumn("name").AsString().NotNullable().WithDefaultValue("")
                .WithColumn("option").AsInt32().NotNullable().WithDefaultValue(-1)
                .WithColumn("wins").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("losses").AsInt32().NotNullable().WithDefaultValue(0);
        }
    }

    public override void Down()
    {
        if (Schema.Table("SLAYER_Duel").Exists())
            Delete.Table("SLAYER_Duel");
    }
}