using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mqtttopictools
{
    // implements some helper functions strictly following their masters source at
    // https://github.com/iosphere/mosquitto/blob/master/lib/util_mosq.c

    // Some names do not meet the C# style to preserve the naming from the master source
    class MQTTTopicTools
    {
        // https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc3901241
        // 4.7.3 Topic semantic and usage

        // from
        // https://github.com/labatrockwell/ofxMosquitto/blob/master/lib/mosquitto/lib/mosquitto.h
        // Enum allows easy debugging.
        public enum Mosq_err : byte
        {
            MOSQ_ERR_SUCCESS = 0,
            MOSQ_ERR_NOMEM = 1,
            MOSQ_ERR_PROTOCOL = 2,
            MOSQ_ERR_INVAL = 3,
            MOSQ_ERR_NO_CONN = 4,
            MOSQ_ERR_CONN_REFUSED = 5,
            MOSQ_ERR_NOT_FOUND = 6,
            MOSQ_ERR_CONN_LOST = 7,
            MOSQ_ERR_SSL = 8,
            MOSQ_ERR_PAYLOAD_SIZE = 9,
            MOSQ_ERR_NOT_SUPPORTED = 10,
            MOSQ_ERR_AUTH = 11,
            MOSQ_ERR_ACL_DENIED = 12,
            MOSQ_ERR_UNKNOWN = 13
        }

        /* Check that a topic used for publishing is valid.
         * Search for + or # in a topic. Return MOSQ_ERR_INVAL if found.
         * Also returns MOSQ_ERR_INVAL if the topic string is too long.
         * Returns MOSQ_ERR_SUCCESS if everything is fine.
         */
        public Mosq_err mosquitto_pub_topic_check(in string str)
        {
            if (str.IndexOf("+") != -1)
            {
                return Mosq_err.MOSQ_ERR_INVAL;
            }

            if (str.IndexOf("#") != -1)
            {
                return Mosq_err.MOSQ_ERR_INVAL;
            }

            if (str.Length > 65535)
            {
                return Mosq_err.MOSQ_ERR_INVAL;
            }

            return Mosq_err.MOSQ_ERR_SUCCESS;
        }

        /* Check that a topic used for subscriptions is valid.
         * Search for + or # in a topic, check they aren't in invalid positions such as
         * foo/#/bar, foo/+bar or foo/bar#.
         * Return MOSQ_ERR_INVAL if invalid position found.
         * Also returns MOSQ_ERR_INVAL if the topic string is too long.
         * Returns MOSQ_ERR_SUCCESS if everything is fine.
         */
        public Mosq_err mosquitto_sub_topic_check(in string str)
        {
            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] == '+')
                {
                    if (((i > 0) && (str[i - 1] != '/')) || ((i < str.Length - 1) && (str[i + 1] != '/')))
                    {
                        return Mosq_err.MOSQ_ERR_INVAL;
                    }
                }
                else
                if (str[i] == '#')
                {
                    if (((i > 0) && (str[i - 1] != '/')) || (i != str.Length - 1))
                    {
                        return Mosq_err.MOSQ_ERR_INVAL;
                    }

                }
            }

            if (str.Length > 65535)
            {
                return Mosq_err.MOSQ_ERR_INVAL;
            }

            return Mosq_err.MOSQ_ERR_SUCCESS;
        }

        /* Does a topic match a subscription? */
        public Mosq_err mosquitto_topic_matches_sub(in string sub, in string topic, out bool result)
        {
            // Hmmm. Looking at the original: is it possible to reach the end without ever setting result?
            result = false;

            if (sub == "" || topic == "") return Mosq_err.MOSQ_ERR_INVAL;

            if (sub.Count() > 0 && topic.Count() > 0)
            {
                if ((sub[0] == '$' && topic[0] != '$') || (topic[0] == '$' && sub[0] != '$'))
                {
                    result = false;
                    return Mosq_err.MOSQ_ERR_SUCCESS;
                }
            }

            // the following is a 1:1 copy from C to C#....
            // Three Hu?s, because C# thinks (and shows) that this code is useless and I'm not sure
            // why it is included...
            bool multilevel_wildcard = false;
            int slen = sub.Length;
            int tlen = topic.Length;
            int spos = 0;
            int tpos = 0;

            while (spos < slen && tpos < tlen)
            {
                if (sub[spos] == topic[tpos])
                {
                    if (tpos == tlen - 1)
                    {
                        /* Check for e.g.foo matching foo /# */
                        if (spos == slen - 3
                                && sub[spos + 1] == '/'
                                && sub[spos + 2] == '#')
                        {
                            result = true;
                            multilevel_wildcard = true; // Hu?
                            return Mosq_err.MOSQ_ERR_SUCCESS;
                        }
                    }
                    spos++;
                    tpos++;
                    if (spos == slen && tpos == tlen)
                    {
                        result = true;
                        return Mosq_err.MOSQ_ERR_SUCCESS;
                    }
                    else if (tpos == tlen && spos == slen - 1 && sub[spos] == '+')
                    {
                        spos++; // Hu?
                        result = true;
                        return Mosq_err.MOSQ_ERR_SUCCESS;
                    }
                }
                else
                {
                    if (sub[spos] == '+')
                    {
                        spos++;
                        while (tpos < tlen && topic[tpos] != '/')
                        {
                            tpos++;
                        }
                        if (tpos == tlen && spos == slen)
                        {
                            result = true;
                            return Mosq_err.MOSQ_ERR_SUCCESS;
                        }
                    }
                    else if (sub[spos] == '#')
                    {
                        multilevel_wildcard = true;    // Hu?
                        if (spos + 1 != slen)
                        {
                            result = false;
                            return Mosq_err.MOSQ_ERR_SUCCESS;
                        }
                        else
                        {
                            result = true;
                            return Mosq_err.MOSQ_ERR_SUCCESS;
                        }
                    }
                    else
                    {
                        result = false;
                        return Mosq_err.MOSQ_ERR_SUCCESS;
                    }
                }
            }
            if (multilevel_wildcard == false && (tpos < tlen || spos < slen))
            {
                result = false;
            }
            return Mosq_err.MOSQ_ERR_SUCCESS;
        }

        // Because that out-thing is inconvenient  
        public bool mosquitto_topic_matches_sub(in string sub, in string topic)
        {
            // Mosq_err erg = mosquitto_topic_matches_sub(sub, topic, out bool res);
            // return (erg == Mosq_err.MOSQ_ERR_SUCCESS && res);

            // Wouldn't say it is impossible to write unreadable code in C# to a certain degree...
            return mosquitto_topic_matches_sub(sub, topic, out bool res) == Mosq_err.MOSQ_ERR_SUCCESS && res;
        }



        public enum hivemq_fail : byte
        {
            hivemq_nofailallgood = 0,
            hivemq_empty = 1,
            hivemq_leadslash = 2,
            hivemq_space = 3,
            hivemq_nonasciichar = 4,
        }

        // https://www.hivemq.com/blog/mqtt-essentials-part-5-mqtt-topics-best-practices/
        public hivemq_fail HiveMQTopicBestPracticeRules(in string topic)
        {
            // OK, they don't say that.
            if (topic=="")
            {
                return hivemq_fail.hivemq_empty;
            }

            // no leading slash
            if (topic[0] == '/')
            {
                return hivemq_fail.hivemq_leadslash;
            }

            // no space
            if (topic.IndexOf(" ")!=-1)
            {
                return hivemq_fail.hivemq_space;
            }

            // no non-ascii-chars
            foreach (char c in topic)
            {
                if (c >= 128)
                {
                    return hivemq_fail.hivemq_nonasciichar;
                }
            }
            return hivemq_fail.hivemq_nofailallgood;
        }



        // *************************************************************************************
        // Test test...
        public void test()
        {
            Mosq_err err = 0;

            err = mosquitto_pub_topic_check("foo/");
            err = mosquitto_pub_topic_check("/foo/");                

            err = mosquitto_pub_topic_check("foo/#");
            err = mosquitto_pub_topic_check("/+/foo");

            err = mosquitto_sub_topic_check("foo/#/bar");
            err = mosquitto_sub_topic_check("foo/+bar");
            err = mosquitto_sub_topic_check("foo/bar#");

            bool serr = false;
            serr = mosquitto_topic_matches_sub("#", "foo");

            hivemq_fail herr = 0;
            herr = HiveMQTopicBestPracticeRules("");
            herr = HiveMQTopicBestPracticeRules("/test");
            herr = HiveMQTopicBestPracticeRules("Täler/Hügel");
            herr = HiveMQTopicBestPracticeRules("not good/Hügel");

        }

    }
}
