using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;

namespace Stream.Api.Stream.Reader.SqlServerDb;

public class BulkInsert
{
    private readonly string connectionString;

    public BulkInsert(string connectionString)
    {
        this.connectionString = connectionString;
    }

    public void InsertData(Dictionary<string, ICollection<(ulong, double)>> data, string tableName)
    {
        try
        {
            using var connection = new SqlConnection(this.connectionString);
            connection.Open();

            // Use SqlBulkCopy to efficiently copy data to the database
            using var bulkCopy = new SqlBulkCopy(connection);
            bulkCopy.DestinationTableName = tableName;
            bulkCopy.BulkCopyTimeout = 0;
            using var dataTable = new DataTable();

            bulkCopy.ColumnMappings.Add("Timestamp", "Timestamp");
            bulkCopy.ColumnMappings.Add("Value", "Value");
            bulkCopy.ColumnMappings.Add("Parameter", "Parameter");

            dataTable.Columns.Add("Timestamp", typeof(ulong));
            dataTable.Columns.Add("Value", typeof(double));
            dataTable.Columns.Add("Parameter", typeof(string));
            foreach (var parameter in data)
            {
                foreach (var item in parameter.Value)
                {
                    var dataRow = dataTable.NewRow();
                    dataRow["Timestamp"] = item.Item1;
                    dataRow["Value"] = item.Item2;
                    dataRow["Parameter"] = parameter.Key.Split(':')[0];
                    dataTable.Rows.Add(dataRow);
                }
            }

            bulkCopy.WriteToServer(dataTable);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
    public void InsertEventData(List<(ulong, string, double, double, double)> eventsData, string tableName)
    {
        try
        {
            using var connection = new SqlConnection(this.connectionString);
            connection.Open();

            // Use SqlBulkCopy to efficiently copy data to the database
            using var bulkCopy = new SqlBulkCopy(connection);
            bulkCopy.DestinationTableName = tableName;
            bulkCopy.BulkCopyTimeout = 0;
            using var dataTable = new DataTable();

            bulkCopy.ColumnMappings.Add("Timestamp", "Timestamp");
            bulkCopy.ColumnMappings.Add("Description", "Description");
            bulkCopy.ColumnMappings.Add("Data1", "Data1");
            bulkCopy.ColumnMappings.Add("Data2", "Data2");
            bulkCopy.ColumnMappings.Add("Data3", "Data3");

            dataTable.Columns.Add("Timestamp", typeof(ulong));
            dataTable.Columns.Add("Description", typeof(string));
            dataTable.Columns.Add("Data1", typeof(double));
            dataTable.Columns.Add("Data2", typeof(double));
            dataTable.Columns.Add("Data3", typeof(double));

            foreach (var eventData in eventsData)
            {
                var dataRow = dataTable.NewRow();
                dataRow["Timestamp"] = eventData.Item1;
                dataRow["Description"] = eventData.Item2;
                dataRow["Data1"] = eventData.Item3;
                dataRow["Data2"] = eventData.Item4;
                dataRow["Data3"] = eventData.Item5;
                dataTable.Rows.Add(dataRow);
            }

            bulkCopy.WriteToServer(dataTable);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public void InsertMarkerData(List<(ulong, string, string)> markersData, string tableName)
    {
        try
        {
            using var connection = new SqlConnection(this.connectionString);
            connection.Open();

            // Use SqlBulkCopy to efficiently copy data to the database
            using var bulkCopy = new SqlBulkCopy(connection);
            bulkCopy.DestinationTableName = tableName;
            bulkCopy.BulkCopyTimeout = 0;
            using var dataTable = new DataTable();

            bulkCopy.ColumnMappings.Add("Timestamp", "Timestamp");
            bulkCopy.ColumnMappings.Add("Label", "Label");
            bulkCopy.ColumnMappings.Add("Type", "Type");

            dataTable.Columns.Add("Timestamp", typeof(ulong));
            dataTable.Columns.Add("Label", typeof(string));
            dataTable.Columns.Add("Type", typeof(string));

            foreach (var markerData in markersData)
            {
                var dataRow = dataTable.NewRow();
                dataRow["Timestamp"] = markerData.Item1;
                dataRow["Label"] = markerData.Item2;
                dataRow["Type"] = markerData.Item3;
                dataTable.Rows.Add(dataRow);
            }

            bulkCopy.WriteToServer(dataTable);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}