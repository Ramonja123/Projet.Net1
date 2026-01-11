using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackendSGH.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEtage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Etage",
                table: "Chambres");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Etage",
                table: "Chambres",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
