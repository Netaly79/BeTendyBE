using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeTendlyBE.Migrations
{
    /// <inheritdoc />
    public partial class MakeMasterSkillsNotNullWithDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at_utc",
                table: "master",
                type: "timestamptz",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<List<string>>(
                name: "skills",
                table: "master",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]",
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "created_at_utc",
                table: "master",
                type: "timestamptz",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "address",
                table: "master",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "about",
                table: "master",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at_utc",
                table: "master",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamptz");

            migrationBuilder.AlterColumn<string>(
                name: "skills",
                table: "master",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(List<string>),
                oldType: "text[]",
                oldDefaultValueSql: "'{}'::text[]");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at_utc",
                table: "master",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamptz");

            migrationBuilder.AlterColumn<string>(
                name: "address",
                table: "master",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "about",
                table: "master",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);
        }
    }
}
