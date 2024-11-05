package AggregateStats

import YardConfig.YardConfigService
import YardConfigMdb.YardNumbers
import YardConfigMdb.YardNumbersService
import humpYardConfiguration.Yard

import java.util.logging.Logger


/**
 * This class is for searching both the IOSDay logs for switch throws
 */
class AgStatsHourlySwitchThrowsService implements AggStatisticsLogMessageProcessorInterface{

    static transactional = true //We will be saving to the database. Better let it roll back changes in case it fails.

    List<SwitchThrowService> sw = [];
    YardConfigService yardConfigService
    private Logger logger;
    Date stats_logDateHourStart
    AgStatsHourlySwitchThrowsService(Logger pLogger, Date pStats_logDateHourStart, String pServerName, String railroadName, String yardName) {
        logger = pLogger;
        stats_logDateHourStart = pStats_logDateHourStart.clone() as Date
        Yard thisYard = YardConfigService.getThisYard()
        yardConfigService = new YardConfigService(railroadName, yardName)
        YardNumbers yardsNumbers = YardNumbersService.getYardNumbers(thisYard.railRoad, thisYard.name)


        //println "This : ${yardsNumbers.yardName}"
        int numberOfSwitches = 100
        for (int s = 0; s <= numberOfSwitches; s++) {
            sw[s] = new SwitchThrowService(logger, railroadName, yardName, s );
        }
    }
    /** Looking for online messages containing switch errors or SWPINFO messages */
    public boolean containsSearchTerms(String l){
        return (l[1] == '+' &&
                ( SwitchThrowService.IosDayLogContainsSearchTermsSWPINFO(l) ||
                        SwitchThrowService.IosDayLogContainsSearchTermSwitchErrors(l)))
    }

    /** Processes IOS Day log line */
    void processLine(String l, String logDateStr, Date statsLogDateHourStart) {
        if (containsSearchTerms(l) == false)
            return;
        //else it contains our search term
        /**************
         * Parse String
         **************/

        //Get Sw Idx
        int s = SwitchThrowService.GetSwIndexFromSwitchMessage(logger, l)

        if (s>0)
            sw[s].ProcessLineForThisSwitch(l, logDateStr, statsLogDateHourStart)

    }


    void saveStats() {
        for (int s = 0; s < sw.size(); s++) {
            sw[s].SaveSwitchThrowStatistics();
        }
    }
}
