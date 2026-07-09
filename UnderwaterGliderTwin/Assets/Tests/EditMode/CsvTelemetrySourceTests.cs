using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class CsvTelemetrySourceTests
    {
        [Test]
        public void Load_ParsesKnownColumnsFromGbkCsv()
        {
            var path = Path.Combine(Application.temporaryCachePath, "sample-telemetry.csv");
            var csv =
                "UTC时间,当前剖面序号,24V电压(V),24V电流(A),消耗电量(AH),运行状态位1,运行状态位2,运行状态位3,运行状态位4,故障状态位1,故障状态位2,警报状态位1,警报状态位2,工作模式,运行状态,原定航程(m),历时(min),转向角(°),活塞位置(mm),DIOL,DIOH,经度(°),纬度(°),深度(m),高度(m),航向(°),纵摇(°),横摇(°),主推进器转速(RPM),目标航段,目标航向(°),目标深度(m),目标高度(m),舱内温度(℃),舱内压力(KPa),CTD盐度(S/m),CTD温度(℃),CTD深度(m),记录/加载进度(%),文件状态位,高压力泵转速(RPM),电池电量(%),外接设备工作状态位,FLBBCD_695,FLBBCD_700,FLBBCD_460,BB3_470,BB3_530,BB3_650,SUNA,CTD_DO\n" +
                "000年00时00分09秒,1,28.5,0.3,0,32,10,0,7,88,3,0,0,水面模式,水面,2,0,32,17,0,16,120.00008333,25.00001728,2.3,100.0,30.4,-2,-5,0,30,44.3,1000,500,29.7,68.6,2.931,26.4086,2.35,0,27,0,95,89,0,5241,0,0,31162,0,0.0,1576\n";
            File.WriteAllText(path, csv, Encoding.GetEncoding(936));

            var result = new CsvTelemetrySource(path).Load();

            Assert.That(result.Frames, Has.Count.EqualTo(1));
            Assert.That(result.SkippedRows, Is.EqualTo(0));
            var frame = result.Frames[0];
            Assert.That(frame.RawTime, Is.EqualTo("000年00时00分09秒"));
            Assert.That(frame.LongitudeDeg, Is.EqualTo(120.00008333).Within(0.00000001));
            Assert.That(frame.LatitudeDeg, Is.EqualTo(25.00001728).Within(0.00000001));
            Assert.That(frame.DepthM, Is.EqualTo(2.3f).Within(0.0001f));
            Assert.That(frame.HeadingDeg, Is.EqualTo(30.4f).Within(0.0001f));
            Assert.That(frame.PitchDeg, Is.EqualTo(-2f).Within(0.0001f));
            Assert.That(frame.RollDeg, Is.EqualTo(-5f).Within(0.0001f));
            Assert.That(frame.WorkMode, Is.EqualTo("水面模式"));
            Assert.That(frame.RunState, Is.EqualTo("水面"));
            Assert.That(frame.BatteryPercent, Is.EqualTo(95f).Within(0.0001f));
        }

        [Test]
        public void Load_SkipsRowsWithInvalidNumbers()
        {
            var path = Path.Combine(Application.temporaryCachePath, "bad-telemetry.csv");
            var csv =
                "UTC时间,当前剖面序号,24V电压(V),24V电流(A),消耗电量(AH),运行状态位1,运行状态位2,运行状态位3,运行状态位4,故障状态位1,故障状态位2,警报状态位1,警报状态位2,工作模式,运行状态,原定航程(m),历时(min),转向角(°),活塞位置(mm),DIOL,DIOH,经度(°),纬度(°),深度(m),高度(m),航向(°),纵摇(°),横摇(°),主推进器转速(RPM),目标航段,目标航向(°),目标深度(m),目标高度(m),舱内温度(℃),舱内压力(KPa),CTD盐度(S/m),CTD温度(℃),CTD深度(m),记录/加载进度(%),文件状态位,高压力泵转速(RPM),电池电量(%),外接设备工作状态位,FLBBCD_695,FLBBCD_700,FLBBCD_460,BB3_470,BB3_530,BB3_650,SUNA,CTD_DO\n" +
                "bad,1,28.5,0.3,0,32,10,0,7,88,3,0,0,水面模式,水面,2,0,32,17,0,16,not-a-number,25.00001728,2.3,100.0,30.4,-2,-5,0,30,44.3,1000,500,29.7,68.6,2.931,26.4086,2.35,0,27,0,95,89,0,5241,0,0,31162,0,0.0,1576\n";
            File.WriteAllText(path, csv, Encoding.GetEncoding(936));

            var result = new CsvTelemetrySource(path).Load();

            Assert.That(result.Frames, Has.Count.EqualTo(0));
            Assert.That(result.SkippedRows, Is.EqualTo(1));
            Assert.That(result.Errors[0], Does.Contain("line 2"));
        }
    }
}
