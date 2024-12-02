using System.Globalization;
using CsvHelper;

public class ReadCSV
{
    public string AUMData()
{
    Console.WriteLine("Get CSV Data as String ...");
    string pathToCsvFile = @"aum.csv";
    using var reader = new StreamReader(pathToCsvFile);
    string csvData = reader.ReadToEnd();
    return csvData;
}

}