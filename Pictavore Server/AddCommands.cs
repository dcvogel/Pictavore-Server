using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASCOM.Pictavore_4021a
{
    public static class AddCommands
    {
        public static void addCommands()
        {
            string[] lines = new string[] 
            {
                ": FINDPOS 0 BCKLSH ! 1 FOCSTP !\r",        //No backlash, 1 step per FOC
                "0 FCSRPOS !\r",
                "BEGIN BOTSW WHILE FOC- REPEAT\r",          //Go down until switch pushed
                "FCSRPOS @ -200 FCSRPOS !\r",               //Save the down count, then set to bottom
                "0 SWAP DO FOC+ LOOP ;\r"                   //Loop back up to original position
            };

            string[] temp = new string[]
            {
                ": TECLO 100 TEC ;\r",
                ": TECMID 200 TEC ;\r",
                ": TECMED 200 TEC ;\r",
                ": TECHI 300 TEC ;\r"
            };

            FTDIio.Connected = true;
            FTDIio.write("\' BOTSW\r");
            string resp = FTDIio.cameraResponse("\n", true);
            //MessageBox.Show(resp);
            if (resp.Contains('?'))
            {
                //MessageBox.Show("Loading new commands");
                FTDIio.sendLineWaitOk("FULLSTP FOCPTR !\r");            //Make focuser full step
                FTDIio.sendLineWaitOk(": BOTSW 592 C@ 16 AND ;\r");     //Return True if switch is open

                for (int i = 0; i < lines.Length; i++)
                {
                    FTDIio.sendLine(lines[i]);
                    //Thread.Sleep(1000);
                }
                FTDIio.waitForOk();

                for (int i = 0; i < temp.Length; i++)
                {
                    FTDIio.sendLineWaitOk(temp[i]);
                }
            }

        }    

    }
}
