using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackendSGH.Migrations
{
    /// <inheritdoc />
    public partial class MakeReservationChambreClientNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReservationChambres_Clients_ClientId",
                table: "ReservationChambres");

            migrationBuilder.AlterColumn<int>(
                name: "ClientId",
                table: "ReservationChambres",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "NomClientNonInscrit",
                table: "ReservationChambres",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ReservationChambres_Clients_ClientId",
                table: "ReservationChambres",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReservationChambres_Clients_ClientId",
                table: "ReservationChambres");

            migrationBuilder.DropColumn(
                name: "NomClientNonInscrit",
                table: "ReservationChambres");

            migrationBuilder.AlterColumn<int>(
                name: "ClientId",
                table: "ReservationChambres",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ReservationChambres_Clients_ClientId",
                table: "ReservationChambres",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
