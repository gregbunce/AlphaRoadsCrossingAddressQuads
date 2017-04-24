using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;

namespace conAlphaRoadsCrossingAddressQuads
{
    class Program
    {
        static FileStream fileStream;
        static StreamWriter streamWriter;


        static void Main(string[] args)
        {
            try
            {
                // get access to the date and time for the text file name
                string strYearMonthDayHourMin = DateTime.Now.ToString("-yyyy-MM-dd-HH-mm");

                // create sql query string for recordset to loop through (remove the top(#) keyword when running outside of testing)
                string strSqlQuery = @"select top(1000) STREETNAME, STREETTYPE, ADDR_SYS from UTRANS_STREETS
                                    where CARTOCODE not in ('1','7','99')
                                    and (HWYNAME = '')
                                    and ((L_F_ADD <> 0 and L_T_ADD <> 0) OR (R_F_ADD <> 0 and R_T_ADD <> 0))
                                    and (STREETNAME like '%[A-Z]%')
                                    and (STREETNAME <> '')
                                    and (STREETNAME not like '%ROUNDABOUT%')
                                    and (STREETNAME not like '% SB')
                                    and (STREETNAME not like '% NB')
                                    group by STREETNAME, STREETTYPE, ADDR_SYS
                                    order by STREETNAME, STREETTYPE, ADDR_SYS;";

                //setup a file stream and a stream writer to write out the road segments
                string path = @"C:\temp\AlphaRoadsCrossAddrGrids" + strYearMonthDayHourMin + ".txt";
                fileStream = new FileStream(path, FileMode.Create);
                streamWriter = new StreamWriter(fileStream);
                // write the first line of the text file - this is the field headings
                streamWriter.WriteLine("ITTR_ID" + "," + "ADDR_SYS" + "," + "STREETNAME" + "," + "STREETTYPE" + "," + "PREDIR_1" + "," + "PREDIR_2" + "," + "PREDIR_3" + "," + "PREDIR_4" + "," + "NOTES");
                int intIttrID = 0;

                // get connection string to sql database from appconfig
                var connectionString = ConfigurationManager.AppSettings["myConn"];

                // get a record set of road segments that need assigned predirs 
                using (SqlConnection con1 = new SqlConnection(connectionString))
                {
                    // open the sqlconnection
                    con1.Open();

                    // create a sqlcommand - allowing for a subset of records from the table
                    using (SqlCommand command1 = new SqlCommand(strSqlQuery, con1))

                    // create a sqldatareader
                    using (SqlDataReader reader1 = command1.ExecuteReader())
                    {
                        if (reader1.HasRows)
                        {
                            // loop through the record set
                            while (reader1.Read())
                            {
                                // itterate the row count
                                intIttrID = intIttrID + 1;

                                // get the current road segments oid
                                //int intRoadOID = Convert.ToInt32(reader1["OBJECTID"]);
                                //int intCount = Convert.ToInt32(reader1["mycount"]);
                                //string strPreDir = reader1["PREDIR"].ToString();
                                string strStreetName = reader1["STREETNAME"].ToString();
                                string strStreetType = reader1["STREETTYPE"].ToString();
                                string strAddrSystem = reader1["ADDR_SYS"].ToString();

                                // check if the unique road crosses the address grid
                                // tuple item1=predir; item2=streetname; item3=streettype; item4=addrsystem
                                Tuple<string, string, string, string, string> tplCrossesAxis = CheckIfRoadCrossesAxis(strStreetName, strStreetType, strAddrSystem);

                                // if this road has at least one predirection in this address grid in the database....
                                if (tplCrossesAxis.Item1 != "-1" & tplCrossesAxis.Item2 != "-1" & tplCrossesAxis.Item3 != "-1" & tplCrossesAxis.Item4 != "-1" & tplCrossesAxis.Item5 != "-1")
                                {
                                    // write out this real-road unique road
                                    if (tplCrossesAxis.Item5 == null | tplCrossesAxis.Item5 == "")
                                    {
                                        streamWriter.WriteLine(intIttrID + "," + strAddrSystem + "," + strStreetName + "," + strStreetType + "," + tplCrossesAxis.Item1 + "," + tplCrossesAxis.Item2 + "," + tplCrossesAxis.Item3 + "," + tplCrossesAxis.Item4 + "," + "");                                        
                                    }
                                    else // more than 4 unique predirs were found for this road (say what?)
                                    {
                                        streamWriter.WriteLine(intIttrID + "," + strAddrSystem + "," + strStreetName + "," + strStreetType + "," + tplCrossesAxis.Item1 + "," + tplCrossesAxis.Item2 + "," + tplCrossesAxis.Item3 + "," + tplCrossesAxis.Item4 + "," + tplCrossesAxis.Item5);

                                    }
                                }
                                else // this road does not have a predirection in this address grid
                                {
                                    streamWriter.WriteLine(intIttrID + "," + strAddrSystem + "," + strStreetName + "," + strStreetType + "," + tplCrossesAxis.Item1 + "," + tplCrossesAxis.Item2 + "," + tplCrossesAxis.Item3 + "," + tplCrossesAxis.Item4 + "," + "NO PREDIRS");                                 
                                }
                            }
                        }
                    }
                }

                //close the stream writer
                streamWriter.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was an error with the conAlphaRoadsCrossingAddressQuads console application, in the Main method." + ex.Message + " " + ex.Source + " " + ex.InnerException + " " + ex.HResult + " " + ex.StackTrace + " " + ex);
                Console.ReadLine();
            }
        }




        // this method checks if the passed-in road crosses the address grid - aka: it has N and S addresses, or it has E and W addresses
        static Tuple<string, string, string, string, string> CheckIfRoadCrossesAxis(string strStName, string strStType, string strAddrSys)
        {
            try
            {
                string strPreDir_x1 = string.Empty;
                string strPreDir_x2 = string.Empty;
                string strPreDir_x3 = string.Empty;
                string strPreDir_x4 = string.Empty;
                string strNotes_x = string.Empty;
                int intPreDirNumber = 0;


                string strQueryStringNearMatchAddr = @"select PREDIR, STREETNAME, STREETTYPE, ADDR_SYS from UTRANS_STREETS
                                                    where STREETNAME = '" + strStName + @"' 
                                                    and STREETTYPE = '" + strStType + @"' 
                                                    and ADDR_SYS = '" + strAddrSys + @"'
                                                    group by PREDIR, STREETNAME, STREETTYPE, ADDR_SYS";

                // get connection string to sql database from appconfig
                var connectionString = ConfigurationManager.AppSettings["myConn"];

                // get a record set of road segments that need assigned predirs 
                using (SqlConnection con3 = new SqlConnection(connectionString))
                {
                    // open the sqlconnection
                    con3.Open();

                    // create a sqlcommand - allowing for a subset of records from the table
                    using (SqlCommand command3 = new SqlCommand(strQueryStringNearMatchAddr, con3))

                    // create a sqldatareader
                    using (SqlDataReader reader3 = command3.ExecuteReader())
                    {
                        if (reader3.HasRows)
                        {
                            // loop through the record set
                            while (reader3.Read())
                            {
                                intPreDirNumber = intPreDirNumber + 1;

                                if (intPreDirNumber == 1)
                                {
                                    strPreDir_x1 = reader3["PREDIR"].ToString();
                                }
                                else if (intPreDirNumber == 2)
                                {
                                    strPreDir_x2 = reader3["PREDIR"].ToString();
                                }
                                else if (intPreDirNumber == 3)
                                {
                                    strPreDir_x3 = reader3["PREDIR"].ToString();
                                }
                                else if (intPreDirNumber == 4)
                                {
                                    strPreDir_x4 = reader3["PREDIR"].ToString();
                                }
                                else if (intPreDirNumber > 4)
                                {
                                    // do something with there are more than four, maybe write this out to a notes field in the txt file
                                    strNotes_x = "MORE THAN FOUR PREDIRS";
                                    break;
                                }
                            }
                        }
                        else
                        {
                            strPreDir_x1 = "-1";
                            strPreDir_x2 = "-1";
                            strPreDir_x3 = "-1";
                            strPreDir_x4 = "-1";
                            strNotes_x = "-1";
                        }
                    }
                }



                return Tuple.Create(strPreDir_x1, strPreDir_x2, strPreDir_x3, strPreDir_x4, strNotes_x);
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was an error with the conAlphaRoadsCrossingAddressQuads console application, in the CheckIfRoadCrossesAxis method." + ex.Message + " " + ex.Source + " " + ex.InnerException + " " + ex.HResult + " " + ex.StackTrace + " " + ex);
                Console.ReadLine();
                return Tuple.Create("-1", "-1", "-1", "-1", "-1");
            }
        
        
        
        }



    }
}
