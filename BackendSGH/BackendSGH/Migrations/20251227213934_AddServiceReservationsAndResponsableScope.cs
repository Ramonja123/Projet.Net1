using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackendSGH.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceReservationsAndResponsableScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReservationServices_Clients_ClientId",
                table: "ReservationServices");

            migrationBuilder.DropColumn(
                name: "HeureDebut",
                table: "ReservationServices");

            migrationBuilder.DropColumn(
                name: "Quantite",
                table: "ReservationServices");

            migrationBuilder.RenameColumn(
                name: "HeureFin",
                table: "ReservationServices",
                newName: "Heure");

            migrationBuilder.RenameColumn(
                name: "DateReservation",
                table: "ReservationServices",
                newName: "Date");

            migrationBuilder.AddColumn<bool>(
                name: "IsResponsableChambre",
                table: "Responsables",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ServiceId",
                table: "Responsables",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Statut",
                table: "ReservationServices",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ClientId",
                table: "ReservationServices",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "NomClientNonInscrit",
                table: "ReservationServices",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Responsables_ServiceId",
                table: "Responsables",
                column: "ServiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReservationServices_Clients_ClientId",
                table: "ReservationServices",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Responsables_Services_ServiceId",
                table: "Responsables",
                column: "ServiceId",
                principalTable: "Services",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReservationServices_Clients_ClientId",
                table: "ReservationServices");

            migrationBuilder.DropForeignKey(
                name: "FK_Responsables_Services_ServiceId",
                table: "Responsables");

            migrationBuilder.DropIndex(
                name: "IX_Responsables_ServiceId",
                table: "Responsables");

            migrationBuilder.DropColumn(
                name: "IsResponsableChambre",
                table: "Responsables");

            migrationBuilder.DropColumn(
                name: "ServiceId",
                table: "Responsables");

            migrationBuilder.DropColumn(
                name: "NomClientNonInscrit",
                table: "ReservationServices");

            migrationBuilder.RenameColumn(
                name: "Heure",
                table: "ReservationServices",
                newName: "HeureFin");

            migrationBuilder.RenameColumn(
                name: "Date",
                table: "ReservationServices",
                newName: "DateReservation");

            migrationBuilder.AlterColumn<string>(
                name: "Statut",
                table: "ReservationServices",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<int>(
                name: "ClientId",
                table: "ReservationServices",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "HeureDebut",
                table: "ReservationServices",
                type: "time",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<int>(
                name: "Quantite",
                table: "ReservationServices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_ReservationServices_Clients_ClientId",
                table: "ReservationServices",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
