﻿// <auto-generated />
using System;
using BenchmarkDotNet.Mathematics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Robust.Benchmarks.Exporters;

#nullable disable

namespace Robust.Benchmarks.Migrations
{
    [DbContext(typeof(BenchmarkContext))]
    partial class BenchmarkContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.4")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Robust.Benchmarks.Exporters.BenchmarkRun", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("GitHash")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("ParameterMapping")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("RunDate")
                        .HasColumnType("timestamptz");

                    b.Property<Statistics>("Statistics")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.HasKey("Id");

                    b.ToTable("BenchmarkRuns");
                });
#pragma warning restore 612, 618
        }
    }
}
