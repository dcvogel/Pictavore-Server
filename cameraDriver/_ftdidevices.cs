using System;
using System.Text;
using System.Diagnostics;
using FTD2XX_NET;
using System.Linq;
using System.Threading;

namespace ASCOM.Pictavore_4021a
{
    public class ftdidevices
    {

        public FTDI io = new FTDI();
        public FTDI term = new FTDI();

        const int rcvWaitTime = 5;

        public ftdidevices()
        {
        }

        public void open()
        {
            FTDI.FT_STATUS statusComm, statusTerm;
            statusComm = io.OpenByIndex(0);
            statusTerm = term.OpenByIndex(1);
            if (!(statusComm == FTDI.FT_STATUS.FT_OK && statusTerm == FTDI.FT_STATUS.FT_OK))
            {
                throw new ASCOM.NotConnectedException("Can't connect");
            }
        }

        public void close()
        {
            FTDI.FT_STATUS statusComm, statusTerm;
            statusComm = io.Close();
            statusTerm = term.Close();
            if (!(statusComm == FTDI.FT_STATUS.FT_OK && statusTerm == FTDI.FT_STATUS.FT_OK))
            {
                throw new ASCOM.NotConnectedException("Error on ftdidevice close");
            }
        }

        public bool stat()
        {
            return term.IsOpen && io.IsOpen;
        }

        public byte[] readio(int size)
        {
            byte[] data = new byte[size];
            uint numBytesRead = 0;

            io.Read(data, size, ref numBytesRead);
            return data;
        }

        public void writeRaw(string cmd)
        {
            uint numBytesWritten = 0;

            term.Write(cmd, cmd.Length, ref numBytesWritten);
        }

        public void sendLine(string s)
        /*
         * Send a command and wait for the echo of the command, but nothing else
         * Return nothing
        */
        {
            string buffer;
            uint numBytesRead = 0;
            uint bytesInBuffer = 0;
            uint numBytesWritten = 0;
            bool timeOut = false;
            StringBuilder response = new StringBuilder();

            term.Purge(FTDI.FT_PURGE.FT_PURGE_RX);                          //Make sure RCV buffer is empty
            term.Write(s, s.Length, ref numBytesWritten);                   //Write string to camera
            Debug.WriteLine("sendLine: " + s);
            DateTime startTime = DateTime.Now;                              //Start time waiting for response
            while ((response.ToString() != s.TrimEnd('\r')) && !timeOut)    //Continue until whole response is received or times out
            {
                term.GetRxBytesAvailable(ref bytesInBuffer);                //Anything in buffer?
                if (bytesInBuffer > 0)                                      //If yes,
                {
                    term.Read(out buffer, 1, ref numBytesRead);             //  get it
                    response.Append(buffer);
                }
                timeOut = ((DateTime.Now - startTime).TotalSeconds)
                                                         > rcvWaitTime;    //Check that it hasn't timed out
            }
            Debug.WriteLine("sendLineResponse: " + response);
        }//end sendLine(...

        public string sendLineWaitOk(string s)
        {
            sendLine(s);
            return cameraResponse();
        }

        private string cameraResponse()
        {
            string buffer;
            uint numBytesRead = 0;
            uint bytesInBuffer = 0;
            bool timeOut = false;
            StringBuilder response = new StringBuilder();
            string r;

            DateTime startTime = DateTime.Now;                              //Start time waiting for response
            while (!response.ToString().EndsWith("OK\r\n") && !timeOut)     //Continue until whole response is received or times out
            {
                term.GetRxBytesAvailable(ref bytesInBuffer);                //Anything in buffer?
                if (bytesInBuffer > 0)                                      //If yes,
                {
                    term.Read(out buffer, bytesInBuffer, ref numBytesRead); //  get it
                    response.Append(buffer);
                }
                timeOut = ((DateTime.Now - startTime).TotalSeconds)
                                                        > rcvWaitTime;      //Check that it hasn't timed out
            }
            Debug.Write("Whole camera response: " + response);
            if (!timeOut) response.Remove(response.Length - 4, 4);          //Remove the trailing "OK\r\n"
            char[] charsToTrim = { ' ', '\r', '\n' };                         //Remove trailing spaces or EOLs (see TEMP?)
            r = response.ToString().TrimEnd(charsToTrim).Split(' ').Last(); //Now, last item after split on space
            Debug.Write("Actual command response: " + r);                   //  is command response
            return r;
        }// end cameraResponse(...



        public void waitForOk(double Duration = 0)
        {
            string buffer;
            uint numBytesRead = 0;
            uint bytesInBuffer = 0;
            double timeOutTime = 0;
            bool timeOut = false;
            StringBuilder response = new StringBuilder();

            DateTime startTime = DateTime.Now;                          //Start time waiting for response
            Debug.WriteLine("Enter waitForOk " + startTime);
            if (Duration > 0) Thread.Sleep((int)(Duration*1000));
            while (!response.ToString().EndsWith("OK\r\n") && !timeOut) //Continue until whole response is received or times out
            {
                term.GetRxBytesAvailable(ref bytesInBuffer);                //Anything in buffer?
                if (bytesInBuffer > 0)                                      //If yes,
                {
                    term.Read(out buffer, bytesInBuffer, ref numBytesRead); //  get it
                    response.Append(buffer);
                }
                timeOutTime = (DateTime.Now - startTime).TotalSeconds;
                timeOut = timeOutTime > 5;    //Check that it hasn't timed out
            }
            Debug.WriteLine("Exit waitForOk " + timeOutTime);
            Debug.WriteLine("waitForOkResponse " + response);
        }


    }
}