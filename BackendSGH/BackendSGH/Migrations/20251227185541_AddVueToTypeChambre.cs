using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackendSGH.Migrations
{
    /// <inheritdoc />
    public partial class AddVueToTypeChambre : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Vue",
                table: "TypeChambres",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Vue",
                table: "TypeChambres");
        }
    }
}
