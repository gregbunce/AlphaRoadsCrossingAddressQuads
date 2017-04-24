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

        // pass in one of the following pair of command line args: STREETNAME STREETTYPE or ALIAS1 ALIAS1TYPE or ALIAS2 ALIAS2TYPE 
        static void Main(string[] args)
        {
            try
            {
                // get access to the date and time for the text file name
                string strYearMonthDayHourMin = DateTime.Now.ToString("-yyyy-MM-dd-HH-mm");

                // create sql query string for recordset to loop through (remove the top(#) keyword when running outside of testing)
                string strSqlQuery = @"select top(100) UTRANS_STREETS." + args[0] + @", UTRANS_STREETS." + args[1] + @", UTRANS_STREETS.ADDR_SYS from UTRANS_STREETS
                                    where CARTOCODE not in ('1','7','99')
                                    and (HWYNAME = '')
                                    and ((L_F_ADD <> 0 and L_T_ADD <> 0) OR (R_F_ADD <> 0 and R_T_ADD <> 0))
                                    and (UTRANS_STREETS." + args[0] + @" like '%[A-Z]%')
                                    and (UTRANS_STREETS." + args[0] + @" <> '')
                                    and (UTRANS_STREETS." + args[0] + @" not like '%ROUNDABOUT%')
                                    and (UTRANS_STREETS." + args[0] + @" not like '% SB')
                                    and (UTRANS_STREETS." + args[0] + @" not like '% NB')
                                    group by UTRANS_STREETS." + args[0] + @", UTRANS_STREETS." + args[1] + @", UTRANS_STREETS.ADDR_SYS
                                    order by UTRANS_STREETS." + args[0] + @", UTRANS_STREETS." + args[1] + @", UTRANS_STREETS.ADDR_SYS;";

                //setup a file stream and a stream writer to write out the road segments
                string path = @"C:\temp\AlphaRoadsCrossAddrGrids_" + args[0] + strYearMonthDayHourMin + ".txt";
                fileStream = new FileStream(path, FileMode.Create);
                streamWriter = new StreamWriter(fileStream);
                // write the first line of the text file - this is the field headings
                streamWriter.WriteLine("ITTR_ID" + "," + "ADDR_SYS" + "," + args[0] + "," + args[1] + "," + "PREDIR_N" + "," + "PREDIR_S" + "," + "PREDIR_E" + "," + "PREDIR_W" + "," + "PREDIR_OTH");
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
                                string strStreetName = reader1[args[0]].ToString();
                                string strStreetType = reader1[args[1]].ToString();
                                string strAddrSystem = reader1["ADDR_SYS"].ToString();

                                // check if the unique road crosses the address grid
                                // tuple item1=predirN; item2=predirS; item3=predirE; item4=predirW; itemspredirOtherValue
                                Tuple<string, string, string, string, string> tplCrossesAxis = CheckIfRoadCrossesAxis(strStreetName, strStreetType, strAddrSystem, args[0], args[1]);

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
                                    streamWriter.WriteLine(intIttrID + "," + strAddrSystem + "," + strStreetName + "," + strStreetType + "," + tplCrossesAxis.Item1 + "," + tplCrossesAxis.Item2 + "," + tplCrossesAxis.Item3 + "," + tplCrossesAxis.Item4 + "," + tplCrossesAxis.Item5);                                 
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
        static Tuple<string, string, string, string, string> CheckIfRoadCrossesAxis(string strStName, string strStType, string strAddrSys, string strArg0, string strArg1)
        {
            try
            {
                string strPreDir_xN = string.Empty;
                string strPreDir_xS = string.Empty;
                string strPreDir_xE = string.Empty;
                string strPreDir_xW = string.Empty;
                string strPreDir_xOther = string.Empty;

                string strQueryStringNearMatchAddr = @"select PREDIR, " + strArg0 + @", " + strArg1 + @", ADDR_SYS from UTRANS_STREETS
                                                    where UTRANS_STREETS." + strArg0 + @" = '" + strStName + @"' 
                                                    and UTRANS_STREETS." + strArg1 + @" = '" + strStType + @"' 
                                                    and UTRANS_STREETS.ADDR_SYS = '" + strAddrSys + @"'
                                                    group by UTRANS_STREETS.PREDIR, UTRANS_STREETS." + strArg0 + @", UTRANS_STREETS." + strArg1 + @", UTRANS_STREETS.ADDR_SYS";
                
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
                                switch (reader3["PREDIR"].ToString().ToUpper().Trim())
                                {
                                    case "N":
                                        strPreDir_xN = "N";
                                        break;
                                    case "S":
                                        strPreDir_xS = "S";
                                        break;
                                    case "E":
                                        strPreDir_xE = "E";
                                        break;
                                    case "W":
                                        strPreDir_xW = "W";
                                        break;

                                    default:
                                        strPreDir_xOther = reader3["PREDIR"].ToString();
                                        break;
                                }
                            }
                        }
                        else
                        {
                            strPreDir_xN = "-1";
                            strPreDir_xS = "-1";
                            strPreDir_xE = "-1";
                            strPreDir_xW = "-1";
                            strPreDir_xOther = "-1";
                        }
                    }
                }

                return Tuple.Create(strPreDir_xN, strPreDir_xS, strPreDir_xE, strPreDir_xW, strPreDir_xOther);
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
